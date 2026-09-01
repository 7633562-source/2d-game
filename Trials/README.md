# Headless-стенд

Из корня проекта. Редактор Unity нужен закрытым только для `-Rebuild`.

```powershell
powershell -Command ".\Tools\run-trial.ps1 -Rebuild -Duration 30 -Label check"
powershell -Command ".\Tools\run-trial.ps1 -Duration 20 -Label pf22 -Extra @('-pushImpulse','22','-pushTime','5')"
powershell -Command ".\Tools\run-trial.ps1 -Duration 20 -Label pb23 -Extra @('-pushImpulse','-23','-pushTime','5')"
```

`+pushImpulse` — вперёд (+X в торс), минус — назад. По умолчанию скрипт передаёт `-fixedDelta 0.005`. Сводки пишутся в `Trials/Runs/<label>.json`. Канон приёмки — `Trials/Baselines/current/`. Старые сводки без fingerprint — `Trials/Baselines/legacy-no-provenance/`.

`buildManifestPath` и `commandArgs` могут содержать абсолютные пути машины: это диагностика, в fingerprint они не входят и после переноса проекта будут другими.
