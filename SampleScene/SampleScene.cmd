@echo off
setlocal
title SampleScene - current version (build bad3088a)
cd /d "E:\Games\2D Game\My project"
if not exist "Build\Headless\BalanceTrial.exe" (echo PLAYER NOT FOUND ^& pause ^& exit /b 1)
echo Reading the build fingerprint...
powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content 'Build\Headless\build-manifest.json' -Raw | ConvertFrom-Json).fingerprint" > "%TEMP%\scenefp.txt" 2>nul
set /p FP=<"%TEMP%\scenefp.txt"
if "%FP%"=="" (echo FINGERPRINT READ FAILED - tell Hermes ^& pause ^& exit /b 2)
echo fingerprint=%FP%
echo Current version launcher (auto-generated; build bad3088a line 3c33feb9). Close the window to stop.
start "" "Build\Headless\BalanceTrial.exe" -trial -duration 3600 -label scene_play -out "Trials\Runs" -sourceFingerprint %FP% -buildManifest "Build\Headless\build-manifest.json" -fixedDelta 0.005 -visual -shotRate 0.2 -poseSwingT 0.600000 -poseStanceT 0.209341 -poseLift 0.064997 -poseLandShare 0.798491 -poseHeightGain 540.5688 -poseHeightDamp 11.72782 -posePlaceMargin 0.019678 -poseLeadSwap 0.075425 -poseStepMin -0.335024 -poseStepMax 0.326438 -poseArmSwing 7.742455 -poseAnkleP 0.554069 -poseAnkleD 0.855518 -poseTailHold 0.010 -poseAnkleCarry -14 -poseAnkleRamp 0.09 -poseBobAmp 0.02 -posePushDeliverP 12 -posePlaceKneeLive 1 -neckP 2.5 -walk 1 -captureWalk 1 -poseDrive 1 -poseWalk 1 -walkPlan "i1.5,w6,i2,w6,i2,s5,r2,w8" -autoGetUp 1.5 -poseGetUpReach 0.35 -poseHandGrip 1 -camAuto 1 -camAutoMargin 0.6 -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "%LOCALAPPDATA%\Temp\grok-burn\scene_play.log"
echo Launched. This small console can be closed.
pause
