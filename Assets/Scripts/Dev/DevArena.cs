#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Battle;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Game;

namespace Emberfall.Dev
{
    /// <summary>
    /// THE DEV ARENA. The real battle view, driving a fight that has not finished.
    ///
    /// WHY IT IS A SCENE AND NOT AN EDITOR WINDOW. The question the tool exists to answer is "does
    /// this read" - does the clip land, does the hit sell, does the kit do what its text says - and
    /// that question cannot be answered in a flat inspector drawing sprites side by side. So this
    /// is a BattleStage with UnitActors on it: the same camera, the same rigs, the same sprite
    /// clips, the same flash and the same death the game uses, because it IS them.
    ///
    /// WHY IT DOES NOT INSTANTIATE BattleScreen. BattleScreen is two things welded together - a
    /// player for an event list, and the campaign chrome around it (App.Session, the router, the
    /// prologue, the recital, the proving). The first half is what an arena wants and the second
    /// half would drag the whole game into a dev scene. So the arena plays the list itself, over
    /// the same presentation primitives, and BattleScreen is not touched at all: the real battle
    /// behaves exactly as it did.
    ///
    /// HOW IT PLAYS INCREMENTALLY. The session appends events as they happen. The player below
    /// holds a cursor into that list and consumes whatever has arrived; when it runs dry it either
    /// waits for a person (Manual) or asks the engine for one more action (Auto). That is the whole
    /// difference between the two modes, which is why switching between them mid-fight costs
    /// nothing and loses nothing.
    /// </summary>
    public sealed class DevArena : MonoBehaviour
    {
        private static readonly string[] ClipIds =
            { "idle", "basic", "basic2", "skill", "hit_reaction", "death", "controlled" };

        private ContentDatabase _content;
        private readonly DevArenaBoard _board = new DevArenaBoard();
        private BattleStage _stage;
        private DevArenaUi _ui;
        private BattleSession _session;

        private bool _auto;
        private bool _playing = true;
        private float _speed = 1f;
        private int _cursor;                 // how far the view has played into the event list
        private bool _stepOnce;              // Auto's "Next action", used while paused

        private int _selected = -1;          // the unit the drawers are about
        private int _pendingActor = -1;      // waiting for a target
        private ManualChoice _pendingChoice = ManualChoice.Basic;

        private readonly List<UnitWidget> _widgets = new List<UnitWidget>();
        private readonly List<Text> _logLines = new List<Text>();
        private readonly List<int[]> _logUnits = new List<int[]>();
        private bool _logExpanded;
        private bool _showAnchors = true;
        private bool _solo;
        private bool _animPaused;
        private bool _animLoop = true;
        private string _animClip = "idle";
        private float _toastUntil;
        private int _highlightA = -1, _highlightB = -1;
        private float _highlightUntil;

        private sealed class UnitWidget
        {
            public int Id;
            public RectTransform Root;
            public Button Tap;
            public Image Frame;
            public Image HpFill;
            public Image ShieldFill;
            public Image EnergyFill;
            public Text Name;
            public Image GroundMark;
            public Image FootMark;
        }

        // ================================================================== boot

        private void Start()
        {
            Application.targetFrameRate = 60;

            // Arriving from the hub's DEV button brings GameApp with it - it is DontDestroyOnLoad,
            // so loading this scene does not unload it. The arena wants the screen to itself.
            if (GameApp.Instance != null) Destroy(GameApp.Instance.gameObject);
            _content = ContentService.Load();
            if (!_board.Load()) _board.SeedDefault(_content);

            GameObject host = new GameObject("ArenaStage");
            host.transform.SetParent(transform, false);
            _stage = host.AddComponent<BattleStage>();
            _stage.Initialise();

            _ui = new DevArenaUi();
            _ui.Build(transform);
            Wire();
            BuildRoster();
            BuildBoardRows();

            StartFight();
            StartCoroutine(Player());
        }

        private void OnDestroy()
        {
            if (_stage != null) _stage.Teardown();
        }

        // ================================================================== the fight

        private void StartFight()
        {
            string problem;
            BattleRequest req = _board.BuildRequest(_content, out problem);
            if (req == null) { Toast(problem); return; }

            _stage.BuildWorld(req.Stage != null ? req.Stage.Environment : "ashen");
            _session = CombatSimulation.Begin(req);
            _stage.Populate(_session.Result);
            _cursor = 0;
            _selected = -1;
            _pendingActor = -1;
            ClearLog();
            BuildUnitWidgets();
            Log("Fight open on " + (req.Stage != null ? req.Stage.Name : "?") + ", seed " + req.Seed
                + ". " + (_auto ? "AUTO" : "MANUAL") + ".", -1, -1);
            RefreshChrome();
        }

        /// <summary>
        /// The one loop. It plays whatever the session has produced that the view has not shown
        /// yet, and when it catches up it either waits for a person or asks for one more action.
        /// </summary>
        private IEnumerator Player()
        {
            while (true)
            {
                if (_session == null) { yield return null; continue; }

                if (_cursor < _session.Events.Count)
                {
                    yield return Handle(_cursor);
                    _cursor++;
                    continue;
                }

                if (_auto && !_session.Finished && (_playing || _stepOnce))
                {
                    _stepOnce = false;
                    _session.StepNatural();
                    RefreshChrome();
                    continue;
                }

                yield return null;
            }
        }

        private IEnumerator Handle(int index)
        {
            BattleEvent e = _session.Events[index];
            UnitActor src = e.SourceId >= 0 ? _stage.Find(e.SourceId) : null;
            UnitActor tgt = e.TargetId >= 0 ? _stage.Find(e.TargetId) : null;

            switch (e.Type)
            {
                case BattleEventType.RoundStart:
                    Log("— round " + e.Value + " —", -1, -1);
                    RefreshChrome();
                    yield return Wait(0.10f);
                    break;

                case BattleEventType.TurnStart:
                    Highlight(e.SourceId, -1, 0.5f);
                    yield return Wait(0.06f);
                    break;

                case BattleEventType.BasicAttack:
                {
                    Log(NameOf(e.SourceId) + "  " + (e.AbilityName ?? "Basic"), e.SourceId, -1);
                    UnitActor victim = _stage.Find(NextStruckBy(index, e.SourceId));
                    if (src != null)
                    {
                        float reach = src.Rig != null && UnitRig.AttacksAtRange(src.Snapshot.Visual.Weapon)
                            ? 0f : UnitActor.ApproachDistance;
                        Vector3 towards = victim != null ? victim.transform.position
                                                         : src.transform.position + Vector3.right;
                        src.PlayAttackLunge(towards, _speed, reach);
                    }
                    yield return Wait(0.16f);
                    break;
                }

                case BattleEventType.AbilityCast:
                    Log(NameOf(e.SourceId) + "  " + (e.AbilityName ?? "Active"), e.SourceId, -1);
                    if (src != null) src.PlayCast(_speed);
                    yield return Wait(0.26f);
                    break;

                case BattleEventType.PassiveTrigger:
                    Log("    passive: " + (e.AbilityName ?? e.AbilityId), e.SourceId, -1);
                    yield return Wait(0.04f);
                    break;

                case BattleEventType.Damage:
                {
                    if (tgt != null)
                    {
                        tgt.DisplayHp = Mathf.Max(0f, tgt.DisplayHp - e.Value);
                        tgt.PlayHit(e.Critical);
                        if (e.Critical) _stage.Shake(0.10f, 0.18f);
                        FloatNumber(tgt, e.Value.ToString("N0"), e.Critical
                            ? new Color(1f, 0.82f, 0.32f, 1f) : new Color(1f, 0.46f, 0.38f, 1f));
                    }
                    Log("    " + NameOf(e.SourceId) + " → " + NameOf(e.TargetId) + "  "
                        + e.Value.ToString("N0") + (e.Critical ? "  CRIT" : ""), e.SourceId, e.TargetId);
                    yield return Wait(0.12f);
                    break;
                }

                case BattleEventType.Heal:
                    if (tgt != null)
                    {
                        tgt.DisplayHp = Mathf.Min(tgt.MaxHp, tgt.DisplayHp + e.Value);
                        FloatNumber(tgt, "+" + e.Value.ToString("N0"), new Color(0.48f, 0.94f, 0.55f, 1f));
                    }
                    Log("    " + NameOf(e.TargetId) + " healed " + e.Value.ToString("N0"), e.SourceId, e.TargetId);
                    yield return Wait(0.10f);
                    break;

                case BattleEventType.ShieldGain:
                    if (tgt != null) tgt.DisplayShield += e.Value;
                    Log("    " + NameOf(e.TargetId) + " gains Shield " + e.Value.ToString("N0"), -1, e.TargetId);
                    yield return Wait(0.08f);
                    break;

                case BattleEventType.ShieldAbsorb:
                    if (tgt != null) tgt.DisplayShield = Mathf.Max(0f, tgt.DisplayShield - e.Value);
                    Log("    " + NameOf(e.TargetId) + "'s shield absorbs " + e.Value.ToString("N0"), -1, e.TargetId);
                    yield return Wait(0.06f);
                    break;

                case BattleEventType.StatusApplied:
                    Log("    " + NameOf(e.TargetId) + " gains " + e.Status + " (" + e.Duration + "r)", e.SourceId, e.TargetId);
                    yield return Wait(0.06f);
                    break;

                case BattleEventType.StatusResisted:
                    Log("    " + NameOf(e.TargetId) + " RESISTS " + e.Status, e.SourceId, e.TargetId);
                    yield return Wait(0.06f);
                    break;

                case BattleEventType.StatusExpired:
                    Log("    " + NameOf(e.TargetId) + " loses " + e.Status, -1, e.TargetId);
                    yield return Wait(0.04f);
                    break;

                case BattleEventType.EnergyChanged:
                    if (tgt != null) tgt.Energy = e.Value;
                    break;

                case BattleEventType.Death:
                    if (tgt != null && tgt.Alive)
                    {
                        tgt.DisplayHp = 0f;
                        StartCoroutine(tgt.PlayDeath(_speed));
                    }
                    Log("    " + NameOf(e.TargetId) + " dies", -1, e.TargetId);
                    yield return Wait(0.22f);
                    break;

                case BattleEventType.CastCancelled:
                    Log("    " + NameOf(e.TargetId) + "'s " + e.AbilityName + " was cancelled", e.SourceId, e.TargetId);
                    yield return Wait(0.10f);
                    break;

                case BattleEventType.BattleEnd:
                    Log(e.Victory ? "— victory —" : "— defeat —", -1, -1);
                    RefreshChrome();
                    break;

                default:
                    yield return null;
                    break;
            }
        }

        /// <summary>Who this action is about to hit, read out of the record rather than guessed, so
        /// the lunge goes where the blow goes.</summary>
        private int NextStruckBy(int from, int sourceId)
        {
            IList<BattleEvent> ev = _session.Events;
            for (int i = from + 1; i < ev.Count && i < from + 24; i++)
            {
                if (ev[i].Type == BattleEventType.TurnStart) break;
                if (ev[i].Type == BattleEventType.Damage && ev[i].SourceId == sourceId) return ev[i].TargetId;
            }
            return -1;
        }

        private IEnumerator Wait(float seconds)
        {
            float t = 0f;
            float span = seconds / Mathf.Max(0.05f, _speed);
            while (t < span) { t += Time.deltaTime; yield return null; }
        }

        // ================================================================== manual actions

        private void OnUnitTapped(int unitId)
        {
            _selected = unitId;

            if (_pendingActor >= 0 && _pendingActor != unitId)
            {
                int actor = _pendingActor;
                _pendingActor = -1;
                _ui.ActionBar.gameObject.SetActive(false);
                Perform(actor, _pendingChoice, unitId);
                RefreshDrawers();
                return;
            }

            if (!_auto)
            {
                UnitView v = _session != null ? _session.Unit(unitId) : null;
                if (v != null && v.Alive) ShowActionBar(unitId, v);
            }
            RefreshDrawers();
        }

        private void ShowActionBar(int unitId, UnitView v)
        {
            _pendingActor = -1;
            _ui.ActionBar.gameObject.SetActive(true);
            _ui.ActionTitle.text = v.Name + "   tap an ability, then a target";
            DevArenaUi.SetLabel(_ui.BasicButton, "BASIC");
            DevArenaUi.SetLabel(_ui.ActiveButton, v.ActiveName.Length > 0 ? "ACTIVE" : "NO ACTIVE");
            _ui.BasicButton.onClick.RemoveAllListeners();
            _ui.ActiveButton.onClick.RemoveAllListeners();
            _ui.BasicButton.onClick.AddListener(delegate { Arm(unitId, ManualChoice.Basic); });
            _ui.ActiveButton.onClick.AddListener(delegate { Arm(unitId, ManualChoice.Active); });
        }

        private void Arm(int actorId, ManualChoice choice)
        {
            _pendingActor = actorId;
            _pendingChoice = choice;
            _ui.ActionTitle.text = NameOf(actorId) + "  " + choice.ToString().ToUpperInvariant()
                                  + "  —  now tap a target";
        }

        private void Perform(int actorId, ManualChoice choice, int targetId)
        {
            if (_session == null) return;
            ActionReport rep = _session.Act(actorId, choice, targetId);
            if (!rep.Accepted)
            {
                Toast(rep.Refusal);
                Log("    refused: " + rep.Refusal, actorId, targetId);
                return;
            }
            for (int i = 0; i < rep.TargetNotes.Count; i++)
            {
                string note = rep.TargetNotes[i];
                if (note.StartsWith("landed on")) continue;
                Toast(note);
                Log("    " + note, actorId, targetId);
            }
            RefreshChrome();
        }

        // ================================================================== chrome and widgets

        private void Wire()
        {
            _ui.ModeButton.onClick.AddListener(delegate { SetMode(!_auto); });
            _ui.PlayButton.onClick.AddListener(delegate { _playing = !_playing; RefreshChrome(); });
            for (int i = 0; i < _ui.SpeedButtons.Count; i++)
            {
                float s = _ui.Speeds[i];
                _ui.SpeedButtons[i].onClick.AddListener(delegate { _speed = s; RefreshChrome(); });
            }
            _ui.NextActionButton.onClick.AddListener(delegate { _stepOnce = true; });
            _ui.EndRoundButton.onClick.AddListener(delegate
            {
                if (_session == null) return;
                _session.EndRound();
                Log("    round ended by hand", -1, -1);
                RefreshChrome();
            });
            _ui.ResetButton.onClick.AddListener(delegate { StartFight(); });
            _ui.RerollButton.onClick.AddListener(delegate
            {
                _board.Seed = UnityEngine.Random.Range(1, 999999);
                _ui.SeedField.text = _board.Seed.ToString();
                StartFight();
            });
            _ui.SeedField.onEndEdit.AddListener(delegate (string s)
            {
                int v;
                if (int.TryParse(s, out v)) _board.Seed = v;
            });
            if (Application.CanStreamedLevelBeLoaded(0))
            {
                Button back = DevArenaUi.Btn("Back", _ui.Root, "BACK", 96f, _ui.Root,
                                             new Vector2(1f, 1f), new Vector2(-160f, -74f),
                                             DevArenaUi.Quiet);
                back.onClick.AddListener(delegate
                { UnityEngine.SceneManagement.SceneManager.LoadScene(0); });
            }

            _ui.HelpButton.onClick.AddListener(delegate
            { _ui.HelpPanel.gameObject.SetActive(!_ui.HelpPanel.gameObject.activeSelf); });
            _ui.HelpClose.onClick.AddListener(delegate { _ui.HelpPanel.gameObject.SetActive(false); });

            _ui.LogExpandButton.onClick.AddListener(delegate
            {
                _logExpanded = !_logExpanded;
                _ui.LogPanel.offsetMax = new Vector2(0f, _logExpanded ? 330f : 104f);
                DevArenaUi.SetLabel(_ui.LogExpandButton, _logExpanded ? "COLLAPSE" : "EXPAND");
            });

            _ui.SetupTab = DevArenaUi.Btn("SetupEdge", _ui.Root, "SETUP", 110f, _ui.Root,
                                          new Vector2(0f, 0.5f), new Vector2(0f, 0f), DevArenaUi.Quiet);
            _ui.SetupTab.onClick.AddListener(delegate { ToggleDrawer(_ui.SetupDrawer); });
            _ui.UnitTab.onClick.AddListener(delegate { ShowRightPage(true); });
            _ui.AnimTab.onClick.AddListener(delegate { ShowRightPage(false); });
            _ui.SetupClose.onClick.AddListener(delegate { _ui.SetupDrawer.gameObject.SetActive(false); });
            _ui.RightClose.onClick.AddListener(delegate { _ui.RightDrawer.gameObject.SetActive(false); });

            Button unitEdge = DevArenaUi.Btn("UnitEdge", _ui.Root, "UNIT", 110f, _ui.Root,
                                             new Vector2(1f, 0.5f), new Vector2(-110f, 0f), DevArenaUi.Quiet);
            unitEdge.onClick.AddListener(delegate { ToggleDrawer(_ui.RightDrawer); });

            _ui.SaveBoardButton.onClick.AddListener(delegate { _board.Save(); Toast("board saved to " + DevArenaBoard.DefaultPath); });
            _ui.LoadBoardButton.onClick.AddListener(delegate { if (_board.Load()) { BuildBoardRows(); StartFight(); } });
            _ui.ClearBoardButton.onClick.AddListener(delegate { _board.Clear(); BuildBoardRows(); });
            _ui.ApplyBoardButton.onClick.AddListener(delegate { StartFight(); });
            _ui.RosterSearch.onValueChanged.AddListener(delegate (string s) { BuildRoster(); });

            _ui.KillButton.onClick.AddListener(delegate { Edit(delegate (int id) { _session.Kill(id); }, "killed"); });
            _ui.ReviveButton.onClick.AddListener(delegate { Edit(delegate (int id) { _session.Revive(id); }, "revived"); });
            _ui.Hp10Button.onClick.AddListener(delegate { Edit(delegate (int id) { _session.SetHpFraction(id, 0.10f); }, "set to 10% HP"); });
            _ui.Hp50Button.onClick.AddListener(delegate { Edit(delegate (int id) { _session.SetHpFraction(id, 0.50f); }, "set to 50% HP"); });
            _ui.HpFullButton.onClick.AddListener(delegate { Edit(delegate (int id) { _session.SetHpFraction(id, 1f); }, "set to full HP"); });
            _ui.EnergyFullButton.onClick.AddListener(delegate { Edit(delegate (int id) { _session.SetEnergy(id, 100); }, "energy filled"); });
            _ui.EnergyZeroButton.onClick.AddListener(delegate { Edit(delegate (int id) { _session.SetEnergy(id, 0); }, "energy emptied"); });

            _ui.AnimPauseButton.onClick.AddListener(delegate { _animPaused = !_animPaused; RefreshAnimPage(); });
            _ui.AnimStepButton.onClick.AddListener(delegate { StepClipFrame(); });
            _ui.AnimLoopButton.onClick.AddListener(delegate { _animLoop = !_animLoop; PlayClip(_animClip); RefreshAnimPage(); });
            _ui.SoloButton.onClick.AddListener(delegate { _solo = !_solo; RefreshSolo(); RefreshAnimPage(); });
            _ui.DetailsButton.onClick.AddListener(delegate
            { _ui.DetailsPanel.gameObject.SetActive(!_ui.DetailsPanel.gameObject.activeSelf); RefreshAnimPage(); });
            _ui.AnimSpeed.onValueChanged.AddListener(delegate (float v) { RefreshAnimPage(); });

            for (int i = 0; i < ClipIds.Length; i++)
            {
                string clip = ClipIds[i];
                float ignored = 0f;
                Button b = DevArenaUi.Btn("Clip_" + clip, _ui.ClipRow, clip, 134f, ref ignored, DevArenaUi.Quiet);
                RectTransform r = b.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                b.onClick.AddListener(delegate { _animClip = clip; PlayClip(clip); RefreshAnimPage(); });
            }
        }

        private void Edit(Action<int> action, string what)
        {
            if (_session == null || _selected < 0) { Toast("select a unit first"); return; }
            action(_selected);
            Log("    [edit] " + NameOf(_selected) + " " + what, -1, _selected);
            RefreshDrawers();
        }

        private void SetMode(bool auto)
        {
            _auto = auto;
            _pendingActor = -1;
            _ui.ActionBar.gameObject.SetActive(false);
            // Auto -> Manual pauses the fight where it stands; Manual -> Auto continues from it.
            // Nothing is rebuilt either way: it is the same session.
            if (!_auto) _playing = false; else _playing = true;
            Log(_auto ? "    switched to AUTO" : "    switched to MANUAL, fight paused in place", -1, -1);
            RefreshChrome();
        }

        private void ToggleDrawer(RectTransform drawer)
        {
            bool on = !drawer.gameObject.activeSelf;
            _ui.SetupDrawer.gameObject.SetActive(false);
            _ui.RightDrawer.gameObject.SetActive(false);
            drawer.gameObject.SetActive(on);
            if (on) RefreshDrawers();
        }

        private void ShowRightPage(bool unit)
        {
            _ui.RightDrawer.gameObject.SetActive(true);
            _ui.UnitPage.gameObject.SetActive(unit);
            _ui.AnimPage.gameObject.SetActive(!unit);
            DevArenaUi.SetColour(_ui.UnitTab, unit ? DevArenaUi.Accent : DevArenaUi.Quiet);
            DevArenaUi.SetColour(_ui.AnimTab, unit ? DevArenaUi.Quiet : DevArenaUi.Accent);
            RefreshDrawers();
        }

        private void RefreshChrome()
        {
            DevArenaUi.SetLabel(_ui.ModeButton, _auto ? "AUTO" : "MANUAL");
            DevArenaUi.SetColour(_ui.ModeButton, _auto ? DevArenaUi.Accent : new Color(0.55f, 0.32f, 0.10f, 1f));
            DevArenaUi.SetLabel(_ui.PlayButton, _playing ? "PAUSE" : "PLAY");
            _ui.PlayButton.gameObject.SetActive(_auto);
            _ui.NextActionButton.gameObject.SetActive(_auto);
            for (int i = 0; i < _ui.SpeedButtons.Count; i++)
            {
                _ui.SpeedButtons[i].gameObject.SetActive(_auto);
                DevArenaUi.SetColour(_ui.SpeedButtons[i],
                    Mathf.Abs(_ui.Speeds[i] - _speed) < 0.01f ? DevArenaUi.Accent : DevArenaUi.Quiet);
            }
            _ui.EndRoundButton.gameObject.SetActive(!_auto);
            // The event cursor is on screen on purpose: "nothing is happening" and "the view has
            // not caught up yet" look identical without it, and telling them apart is the first
            // thing anyone asks when a fight seems stuck.
            _ui.RoundLabel.text = _session == null ? "—"
                : (_session.Finished ? "FINISHED" : "ROUND " + Mathf.Max(1, _session.Round))
                  + (_session == null ? "" : "   ev " + _cursor + "/" + _session.Events.Count);
            if (_ui.SeedField != null && _ui.SeedField.text != _board.Seed.ToString())
                _ui.SeedField.text = _board.Seed.ToString();
        }

        private void RefreshDrawers()
        {
            if (_session == null) return;
            UnitView v = _selected >= 0 ? _session.Unit(_selected) : null;
            if (v == null)
            {
                _ui.UnitTitle.text = "no unit selected";
                _ui.UnitStats.text = "";
                _ui.UnitStatuses.text = "";
                return;
            }
            _ui.UnitTitle.text = v.Name + (v.Alive ? "" : "   (dead)");
            _ui.UnitStats.text =
                "HP        " + Mathf.RoundToInt(v.Hp).ToString("N0") + " / " + Mathf.RoundToInt(v.MaxHp).ToString("N0")
                + (v.Shield > 0.5f ? "   + shield " + Mathf.RoundToInt(v.Shield).ToString("N0") : "") + "\n"
                + "ENERGY    " + v.Energy + " / " + v.EnergyMax
                + (v.ActiveName.Length > 0 ? "   (" + v.ActiveName + " costs " + v.ActiveCost + ")" : "") + "\n"
                + "ATTACK    " + Mathf.RoundToInt(v.Attack).ToString("N0") + "\n"
                + "DEFENSE   " + Mathf.RoundToInt(v.Defense).ToString("N0") + "\n"
                + "SPEED     " + Mathf.RoundToInt(v.Speed).ToString("N0") + "\n"
                + "CRIT      " + Mathf.RoundToInt(v.CritChance * 100f) + "%  x" + v.CritDamage.ToString("0.00") + "\n"
                + "BASIC     " + v.BasicName;

            if (v.Statuses.Count == 0) _ui.UnitStatuses.text = "no statuses";
            else
            {
                string s = "STATUSES\n";
                for (int i = 0; i < v.Statuses.Count; i++)
                {
                    StatusView st = v.Statuses[i];
                    s += "  " + st.Kind + (st.Stacks > 1 ? " x" + st.Stacks : "")
                         + "   " + st.RoundsLeft + "r"
                         + (Mathf.Abs(st.Magnitude) > 0.0001f ? "   mag " + st.Magnitude.ToString("0.###") : "")
                         + (st.Counter != 0 ? "   charges " + st.Counter : "")
                         + (Mathf.Abs(st.Stored) > 0.0001f ? "   stored " + st.Stored.ToString("0.#") : "")
                         + "\n";
                }
                _ui.UnitStatuses.text = s;
            }
            RefreshAnimPage();
        }

        private void RefreshAnimPage()
        {
            DevArenaUi.SetLabel(_ui.AnimPauseButton, _animPaused ? "RESUME" : "PAUSE");
            DevArenaUi.SetColour(_ui.AnimLoopButton, _animLoop ? DevArenaUi.Accent : DevArenaUi.Quiet);
            DevArenaUi.SetColour(_ui.SoloButton, _solo ? DevArenaUi.Accent : DevArenaUi.Quiet);
            _ui.AnimSpeedLabel.text = "clip speed " + _ui.AnimSpeed.value.ToString("0.00") + "x"
                                      + (_animPaused ? "   (paused — STEP advances one frame)" : "");

            if (!_ui.DetailsPanel.gameObject.activeSelf) return;
            UnitActor a = _selected >= 0 ? _stage.Find(_selected) : null;
            if (a == null) { _ui.ClipDetails.text = ""; return; }
            string sprite = a.Snapshot.Visual.Sprite;
            string text = "DERIVED, PER CLIP\n";
            for (int i = 0; i < ClipIds.Length; i++)
            {
                SpriteAnimClip c = BattleAssets.UnitAnimClip(sprite, ClipIds[i]);
                text += "  " + ClipIds[i].PadRight(14)
                        + (c == null ? "—" :
                           "frames " + c.FrameCount + "   fps " + c.Fps.ToString("0.#")
                           + " x" + c.PlaybackSpeed.ToString("0.00")
                           + "   foot " + (c.FootFraction >= 0f ? c.FootFraction.ToString("0.000") : "(hero)")
                           + "   impact " + c.ImpactFraction.ToString("0.00"))
                        + "\n";
            }
            _ui.ClipDetails.text = text;
        }

        private void PlayClip(string clipId)
        {
            UnitActor a = _selected >= 0 ? _stage.Find(_selected) : null;
            if (a == null || a.Rig == null || a.Rig.Animation == null)
            { Toast("that unit has no sprite clips"); return; }
            if (_animLoop) a.Rig.Animation.PlayLoop(clipId);
            else a.Rig.Animation.PlayOnce(clipId, true);
        }

        /// <summary>
        /// One frame forward while the clip clock is held. The animator advances on the deltaTime
        /// it is handed, so a single nudge of one frame's worth of time is exactly one frame.
        /// </summary>
        private void StepClipFrame()
        {
            UnitActor a = _selected >= 0 ? _stage.Find(_selected) : null;
            if (a == null || a.Rig == null || a.Rig.Animation == null) return;
            SpriteAnimClip c = BattleAssets.UnitAnimClip(a.Snapshot.Visual.Sprite, _animClip);
            float fps = c != null ? Mathf.Max(1f, c.Fps * c.PlaybackSpeed) : 30f;
            a.Rig.Animation.Update(1f / fps);
        }

        private void RefreshSolo()
        {
            for (int i = 0; i < _stage.Actors.Count; i++)
            {
                UnitActor a = _stage.Actors[i];
                bool on = !_solo || a.Snapshot.Id == _selected;
                if (a.Rig != null && a.Rig.Root != null) a.Rig.Root.gameObject.SetActive(on);
            }
        }

        // ================================================================== per-unit overlay

        private void BuildUnitWidgets()
        {
            for (int i = 0; i < _widgets.Count; i++)
                if (_widgets[i].Root != null) Destroy(_widgets[i].Root.gameObject);
            _widgets.Clear();
            if (_session == null) return;

            IList<UnitView> views = _session.Units;
            for (int i = 0; i < views.Count; i++)
            {
                UnitView v = views[i];
                UnitWidget w = new UnitWidget();
                w.Id = v.Id;
                w.Root = DevArenaUi.Blank("Unit_" + v.Id, _ui.UnitLayer);
                w.Root.sizeDelta = new Vector2(150f, 150f);
                w.Root.anchorMin = w.Root.anchorMax = new Vector2(0f, 0f);

                Image tap = DevArenaUi.Panel("Tap", w.Root, new Color(1f, 1f, 1f, 0.01f));
                tap.rectTransform.sizeDelta = new Vector2(132f, 150f);
                tap.rectTransform.anchoredPosition = new Vector2(0f, 6f);
                w.Tap = tap.gameObject.AddComponent<Button>();
                int id = v.Id;
                w.Tap.onClick.AddListener(delegate { OnUnitTapped(id); });

                w.Frame = DevArenaUi.Panel("Frame", w.Root, new Color(0.30f, 0.72f, 1f, 0f));
                w.Frame.rectTransform.sizeDelta = new Vector2(136f, 6f);
                w.Frame.rectTransform.anchoredPosition = new Vector2(0f, 84f);
                w.Frame.raycastTarget = false;

                Image hpTrack = DevArenaUi.Panel("HpTrack", w.Root, new Color(0.05f, 0.05f, 0.07f, 0.85f));
                hpTrack.rectTransform.sizeDelta = new Vector2(128f, 12f);
                hpTrack.rectTransform.anchoredPosition = new Vector2(0f, 70f);
                hpTrack.raycastTarget = false;
                w.ShieldFill = Fill(hpTrack.rectTransform, new Color(0.62f, 0.78f, 0.98f, 0.95f));
                w.HpFill = Fill(hpTrack.rectTransform, v.IsPlayer
                    ? new Color(0.36f, 0.82f, 0.45f, 1f) : new Color(0.86f, 0.32f, 0.30f, 1f));

                Image enTrack = DevArenaUi.Panel("EnTrack", w.Root, new Color(0.05f, 0.05f, 0.07f, 0.85f));
                enTrack.rectTransform.sizeDelta = new Vector2(128f, 6f);
                enTrack.rectTransform.anchoredPosition = new Vector2(0f, 60f);
                enTrack.raycastTarget = false;
                w.EnergyFill = Fill(enTrack.rectTransform, new Color(0.98f, 0.80f, 0.32f, 1f));

                w.Name = DevArenaUi.Label("Name", w.Root, v.Name, 14, DevArenaUi.Text0,
                                          TextAnchor.MiddleCenter, 0f, 160f);
                w.Name.rectTransform.anchorMin = w.Name.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                w.Name.rectTransform.sizeDelta = new Vector2(160f, 18f);
                w.Name.rectTransform.anchoredPosition = new Vector2(0f, 88f);

                w.GroundMark = DevArenaUi.Panel("Ground", w.Root, new Color(1f, 0.58f, 0.18f, 0.95f));
                w.GroundMark.rectTransform.sizeDelta = new Vector2(46f, 3f);
                w.GroundMark.raycastTarget = false;
                w.FootMark = DevArenaUi.Panel("Foot", w.Root, new Color(1f, 0.30f, 0.30f, 0.6f));
                w.FootMark.rectTransform.sizeDelta = new Vector2(88f, 2f);
                w.FootMark.raycastTarget = false;

                _widgets.Add(w);
            }
        }

        private static Image Fill(RectTransform track, Color colour)
        {
            Image f = DevArenaUi.Panel("Fill", track, colour);
            RectTransform r = f.rectTransform;
            r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0f, 0.5f);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillAmount = 1f;
            f.raycastTarget = false;
            return f;
        }

        private void LateUpdate()
        {
            if (_session == null || _stage == null || _ui == null) return;
            RefreshChrome();

            float scale = _ui.Canvas != null ? _ui.Canvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;

            for (int i = 0; i < _widgets.Count; i++)
            {
                UnitWidget w = _widgets[i];
                UnitActor a = _stage.Find(w.Id);
                UnitView v = _session.Unit(w.Id);
                if (a == null || v == null) { w.Root.gameObject.SetActive(false); continue; }

                bool visible = !_solo || w.Id == _selected;
                w.Root.gameObject.SetActive(visible);
                if (!visible) continue;

                Vector3 basePoint = _stage.ScreenPoint(a.transform.position);
                w.Root.anchoredPosition = new Vector2(basePoint.x / scale, basePoint.y / scale);

                float max = Mathf.Max(1f, v.MaxHp);
                w.HpFill.fillAmount = Mathf.Clamp01(v.Hp / max);
                w.ShieldFill.fillAmount = Mathf.Clamp01((v.Hp + v.Shield) / max);
                w.EnergyFill.fillAmount = Mathf.Clamp01(v.Energy / Mathf.Max(1f, v.EnergyMax));
                w.Name.color = v.Alive ? DevArenaUi.Text0 : new Color(0.5f, 0.5f, 0.55f, 1f);

                bool lit = (w.Id == _highlightA || w.Id == _highlightB) && Time.time < _highlightUntil;
                bool sel = w.Id == _selected;
                Color frame = w.Frame.color;
                frame.a = lit ? 0.95f : (sel ? 0.55f : 0f);
                w.Frame.color = frame;

                w.GroundMark.gameObject.SetActive(_showAnchors);
                w.FootMark.gameObject.SetActive(_showAnchors);
                if (_showAnchors)
                {
                    w.GroundMark.rectTransform.anchoredPosition = Vector2.zero;
                    Renderer rend = a.GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        Vector3 bottom = new Vector3(a.transform.position.x, rend.bounds.min.y, a.transform.position.z);
                        Vector3 sp = _stage.ScreenPoint(bottom);
                        w.FootMark.rectTransform.anchoredPosition =
                            new Vector2(0f, sp.y / scale - basePoint.y / scale);
                    }
                }
            }

            if (_ui.ActionBar.gameObject.activeSelf)
            {
                UnitActor a = _stage.Find(_pendingActor >= 0 ? _pendingActor : _selected);
                if (a != null)
                {
                    Vector3 p = _stage.ScreenPoint(a.transform.position);
                    _ui.ActionBar.anchoredPosition = new Vector2(p.x / scale, Mathf.Max(112f, p.y / scale - 104f));
                }
            }

            if (_ui.ToastGroup.alpha > 0f && Time.time > _toastUntil)
                _ui.ToastGroup.alpha = Mathf.Max(0f, _ui.ToastGroup.alpha - Time.deltaTime * 1.6f);

            if (_animPaused)
            {
                UnitActor a = _selected >= 0 ? _stage.Find(_selected) : null;
                if (a != null && a.Rig != null && a.Rig.Animation != null)
                    a.Rig.Animation.Update(-Time.deltaTime);   // cancel this frame's own advance
            }
        }

        // ================================================================== setup drawer contents

        private void BuildBoardRows()
        {
            for (int i = _ui.BoardRows.childCount - 1; i >= 0; i--)
                Destroy(_ui.BoardRows.GetChild(i).gameObject);

            AddBoardHeading("ALLIES");
            for (int i = 0; i < DevArenaBoard.SlotsPerSide; i++) AddBoardRow(true, i);
            AddBoardHeading("ENEMIES (heroes, mirrored)");
            for (int i = 0; i < DevArenaBoard.SlotsPerSide; i++) AddBoardRow(false, i);
        }

        private void AddBoardHeading(string text)
        {
            Text t = DevArenaUi.Label("Heading", _ui.BoardRows, text, 15, DevArenaUi.Text1,
                                      TextAnchor.MiddleLeft, 0f, 400f);
            LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;
        }

        private void AddBoardRow(bool ally, int index)
        {
            DevArenaBoard.Slot slot = _board.At(ally, index);
            RectTransform row = DevArenaUi.Blank("Row", _ui.BoardRows);
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 30f;

            string name = slot.Empty ? "(empty)" : ShortName(slot.DefId);
            float x = 0f;
            Button pick = DevArenaUi.Btn("Pick", row, (index + 1) + "  " + name, 250f, ref x,
                                         _selectedSlotAlly == ally && _selectedSlotIndex == index
                                             ? DevArenaUi.Accent : DevArenaUi.Quiet);
            pick.GetComponent<RectTransform>().sizeDelta = new Vector2(250f, 28f);
            bool a2 = ally; int i2 = index;
            pick.onClick.AddListener(delegate
            {
                _selectedSlotAlly = a2; _selectedSlotIndex = i2; BuildBoardRows();
            });

            Button lvl = DevArenaUi.Btn("Lvl", row, "L" + slot.Level, 66f, ref x, DevArenaUi.Quiet);
            lvl.GetComponent<RectTransform>().sizeDelta = new Vector2(66f, 28f);
            lvl.onClick.AddListener(delegate
            {
                slot.Level = slot.Level >= 80 ? 1 : slot.Level + 10;
                BuildBoardRows();
            });

            Button tal = DevArenaUi.Btn("Tal", row, TalentLabel(slot.Talent), 58f, ref x, DevArenaUi.Quiet);
            tal.GetComponent<RectTransform>().sizeDelta = new Vector2(58f, 28f);
            tal.onClick.AddListener(delegate { slot.Talent = (slot.Talent + 1) % 4; BuildBoardRows(); });

            Button clr = DevArenaUi.Btn("Clr", row, "x", 30f, ref x, DevArenaUi.Quiet);
            clr.GetComponent<RectTransform>().sizeDelta = new Vector2(30f, 28f);
            clr.onClick.AddListener(delegate
            {
                DevArenaBoard.Slot s = _board.At(a2, i2);
                s.DefId = ""; s.RelicId = "";
                BuildBoardRows();
            });
        }

        private static string TalentLabel(int t)
        {
            return t == 0 ? "—" : (t == 1 ? "I" : (t == 2 ? "II" : "III"));
        }

        private bool _selectedSlotAlly = true;
        private int _selectedSlotIndex;

        private void BuildRoster()
        {
            if (_ui == null || _ui.RosterContent == null) return;
            for (int i = _ui.RosterContent.childCount - 1; i >= 0; i--)
                Destroy(_ui.RosterContent.GetChild(i).gameObject);
            if (_content == null) return;

            string filter = _ui.RosterSearch != null ? _ui.RosterSearch.text : "";
            int shown = 0;
            for (int i = 0; i < _content.Heroes.Count && shown < 120; i++)
            {
                HeroDef h = _content.Heroes[i];
                if (!string.IsNullOrEmpty(filter)
                    && h.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && h.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                bool art = BattleAssets.UnitAnimClip(h.Visual.Sprite, "idle") != null;
                float x = 0f;
                Button b = DevArenaUi.Btn("Hero_" + h.Id, _ui.RosterContent,
                                          (art ? "◆ " : "   ") + h.Name + "   " + h.Id, 400f, ref x,
                                          art ? DevArenaUi.Quiet : new Color(0.08f, 0.08f, 0.10f, 1f));
                LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 30f;
                b.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 30f);
                string id = h.Id;
                b.onClick.AddListener(delegate
                {
                    _board.At(_selectedSlotAlly, _selectedSlotIndex).DefId = id;
                    BuildBoardRows();
                });
                shown++;
            }
        }

        // ================================================================== log and toast

        private void ClearLog()
        {
            for (int i = 0; i < _logLines.Count; i++)
                if (_logLines[i] != null) Destroy(_logLines[i].gameObject);
            _logLines.Clear();
            _logUnits.Clear();
        }

        private void Log(string text, int a, int b)
        {
            Text t = DevArenaUi.Label("Line", _ui.LogContent, text, 16, DevArenaUi.Text0,
                                      TextAnchor.MiddleLeft, 0f, 1400f);
            t.raycastTarget = true;
            LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 20f;
            Button tap = t.gameObject.AddComponent<Button>();
            int ia = a, ib = b;
            tap.onClick.AddListener(delegate { Highlight(ia, ib, 2.5f); });

            _logLines.Add(t);
            _logUnits.Add(new[] { a, b });
            while (_logLines.Count > 220)
            {
                if (_logLines[0] != null) Destroy(_logLines[0].gameObject);
                _logLines.RemoveAt(0);
                _logUnits.RemoveAt(0);
            }
        }

        private void Highlight(int a, int b, float seconds)
        {
            _highlightA = a; _highlightB = b;
            _highlightUntil = Time.time + seconds;
            if (a >= 0) _selected = a;
            RefreshDrawers();
        }

        private void Toast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _ui.Toast.text = text;
            _ui.ToastGroup.alpha = 1f;
            _toastUntil = Time.time + 3.2f;
        }

        private string NameOf(int id)
        {
            if (id < 0) return "—";
            UnitView v = _session != null ? _session.Unit(id) : null;
            return v != null ? v.Name : ("#" + id);
        }

        private string ShortName(string defId)
        {
            HeroDef d = _content != null ? _content.GetHero(defId) : null;
            return d != null ? d.Name : defId;
        }

        // ================================================================== floating numbers

        private void FloatNumber(UnitActor actor, string text, Color colour)
        {
            Text t = DevArenaUi.Label("Float", _ui.UnitLayer, text, 26, colour, TextAnchor.MiddleCenter, 0f, 200f);
            RectTransform r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
            r.sizeDelta = new Vector2(200f, 34f);
            StartCoroutine(FloatRoutine(t, actor));
        }

        private IEnumerator FloatRoutine(Text t, UnitActor actor)
        {
            float scale = _ui.Canvas != null ? _ui.Canvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;
            float life = 0.9f;
            float age = 0f;
            while (age < life && t != null && actor != null)
            {
                age += Time.deltaTime * Mathf.Max(0.25f, _speed);
                float k = age / life;
                Vector3 p = _stage.ScreenPoint(actor.CenterPosition);
                t.rectTransform.anchoredPosition = new Vector2(p.x / scale, p.y / scale + 36f + k * 60f);
                Color c = t.color;
                c.a = 1f - k * k;
                t.color = c;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }
    }
}
#endif
