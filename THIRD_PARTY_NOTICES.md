# Third-party notices

Code adapted or copied from other projects, and the license terms that
permit it. See each listed file's own header comment for specifics on
what was adapted vs. copied verbatim.

## Road Builder (MIT)

https://github.com/JadHajjar/RoadBuilder-CSII

```
MIT License

Copyright (c) 2024 JadHajjar

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Files in this repo adapted or copied from it:

- `src/UI/ExtendedUISystemBase.cs` - adapted from `RoadBuilder/Systems/UI/ExtendedUISystemBase.cs`. Namespace and the `Group` constant changed for this project; every API it depends on re-confirmed against this project's own installed game DLLs (see the file's own header and DECISIONS.md).
- `UI/src/vanilla/TextInputTheme.ts` - copied from the pattern in `RoadBuilder/UI/src/mods/Components/TextInput/TextInput.tsx` (the `TextInputTheme` export specifically - a one-off `getModule` call Road Builder itself doesn't route through its resolver either).
- `UI/src/shared/base.scss` - the `textInputBasic` mixin is copied from `RoadBuilder/UI/src/mods/base.scss`, kept in its own clearly-labeled section (see that file's own comment) rather than merged into this project's pre-existing, independently-written spacing scale.

## Find It (all rights reserved)

https://github.com/JadHajjar/FindIt-CSII - no license. Nothing has been
copied from this project; only its publicly observable *patterns* (which
vanilla module paths it reads, its docking approach) have ever informed
a design discussion - see `UI_STYLE_GUIDE.md` and `UI_OVERHAUL_PLAN.md`
for where that's referenced. Reimplemented independently wherever used,
never copied.
