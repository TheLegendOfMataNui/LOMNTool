﻿LOMNTool
========

Usage Notes
-----------

- Bionicle: The Legend of Mata Nui requires all materials to have a texture mapped to them.

Design Goal
-----------

LOMNTool should be used by dragging and dropping an assortment of files onto the executable, and it should process each file out to a similarly named file in turn. Because the command-line arguments are used up by the input filenames, processing options should be set in an INI configuration file, `LOMNTool.ini`.

TODO
----
 - [X] Config reading / writing
 - [X] Legacy DirectX Mesh reading / writing (.x files)
   - [X] Static
     - [X] Reading
     - [X] Importing from OBJ
     - [X] Importing from COLLADA
   - [X] Skinned
     - [X] Reading
     - [X] Importing from COLLADA
   - [ ] Animated? I know LOMN doesn't store animation sequences in the .x files, but it might be useful to have.
 - [ ] BKD Animation
   - [ ] Reading (COLALDA export)
   - [ ] Writing (COLLADA import)
 - [ ] DDS Textures (just so people don't have to find an extra tool for them)
   - [ ] Reading (PNG export)
   - [ ] Writing (PNG import)
 - [ ] BHD reading / writing
   - [X] Reading
   - [ ] Writing
   - [ ] Creating
 - [ ] CDX collision investigation
 - [ ] SLB investigation
 - [ ] OSI bytecode
   - [ ] Decompiler
   - [ ] Compiler
 - [ ] BCL meshes
   - [X] OBJ Export
   - [X] OBJ Import
   - [ ] DAE Import
 - [X] OCL collision