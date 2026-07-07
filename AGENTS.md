# Cross-project data contract

These repositories form one data pipeline and must be reviewed together:

- Game runtime: sibling repository `../Bullet-Hell`
- BulletBuffer generator: sibling repository `../BulletBufferMaker`
- StageData generator: sibling repository `../Bullet-Hell-StageDataMaker`

For every significant or breaking change involving StageData JSON, BulletBuffer JSON, their loaders, validation, naming, timing, boss/combat fields, or cross-references:

1. Inspect all three repositories in the same task.
2. Update the runtime parser/model and the applicable generator model, generator values, and validation together.
3. If a schema is unaffected in one repository, explicitly verify and report that fact; do not add unrelated fields.
4. Build the affected .NET generator projects and validate representative generated JSON before considering the change complete.
5. Preserve unrelated uncommitted work in every repository.
