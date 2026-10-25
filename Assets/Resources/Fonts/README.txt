Emberfall UI typefaces
======================

Two faces, both SIL Open Font License 1.1, both from Google Fonts. The licence for each sits
beside it in this folder and must stay there: OFL 1.1 requires the licence to travel with the
font, and this folder is inside Resources, so it travels into the build.

  Cinzel.ttf                    titles, ribbons, display figures
  Cinzel-OFL.txt                its licence
                                Copyright 2020 The Cinzel Project Authors
                                https://github.com/NDISCOVER/Cinzel
                                This is the VARIABLE font (the upstream file is named with a
                                [wght] axis in its filename). It is renamed here because a
                                Resources path containing square brackets is a path this project
                                would have to escape at every call site. Unity's dynamic font
                                rasteriser renders its default instance, which is the regular
                                weight, and that is the weight the kit asks for.

  BarlowCondensed-SemiBold.ttf  every label, caption, stat and currency figure
  BarlowCondensed-Regular.ttf   body copy, where SemiBold is too loud
  BarlowCondensed-OFL.txt       their licence
                                Copyright 2017 The Barlow Project Authors
                                https://github.com/jpt/barlow

R55 added the three faces of the A2 screens, also SIL Open Font License 1.1, from Google Fonts:

  LilitaOne-Regular.ttf         titles, buttons and numbers (the chunky voice)
  OFL_Lilita.txt                its licence
  Nunito-Regular.ttf            captions and hints
  Nunito-Bold.ttf               names, chips and stat lines
  OFL_Nunito.txt                their licence

Nothing loads the Cinzel and Barlow files by filename except Emberforge.TitleFont and
Emberforge.UiFont. Renaming one means editing those two properties and nothing else. The A2 fonts
are loaded by A2Kit.Display, A2Kit.Body and A2Kit.BodyBold from exactly these three filenames, and
A2Kit.FontReport() says which of them loaded and which fell back to Barlow.

WHY THESE ARE LEGACY Font ASSETS AND NOT TMP FONT ASSETS is written out in full in
Assets/Scripts/UI/Emberforge.cs, under the type section. The short version: every screen in this
project draws text with UnityEngine.UI.Text, there is no TextMeshPro in the tree, and a TMP font
asset can only be drawn by a TMP_Text component. These two .ttf files are the input to a TMP font
asset if the project ever moves, so nothing here has to be re-sourced on that day.
