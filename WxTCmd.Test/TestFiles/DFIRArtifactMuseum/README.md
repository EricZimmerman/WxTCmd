# DFIR Artifact Museum samples

From https://github.com/AndrewRathbun/DFIRArtifactMuseum (MIT license), `Windows/WindowsTimeline`:

- `Win11/RathbunVM/ActivitiesCache.db.gz` with `ActivitiesCache.db-wal.gz`: Windows 11; the database file is 4 KB and every row is in the WAL
- `Win10/BelkasoftCTF_InsiderThreat_L.User/ActivitiesCache.db.gz` (`L.User` in the Museum): Windows 10, with Cyrillic text in its payloads

They are gzipped; the tests decompress them.
