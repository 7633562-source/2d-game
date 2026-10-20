@echo off
setlocal
title the game - balance assist ON (build ed94f382)
cd /d "E:\Games\2D Game\My project"
:watch
if not exist "Build\Headless\BalanceTrial.exe" (echo PLAYER NOT FOUND ^& pause ^& exit /b 1)
set OWNER_EXE=Build\Owner\BalanceTrial.exe
robocopy "Build\Headless" "Build\Owner" /MIR /XD trials Tools *DoNotShip* *BackUpThisFolder* /XF *.stale-* *.log >nul
if not exist "%OWNER_EXE%" (echo OWNER PLAYER COPY FAILED ^& pause ^& exit /b 3)
set FP=
for /f "tokens=2 delims=:, " %%a in ('findstr /c:"\"fingerprint\"" "Build\Owner\build-manifest.json"') do set FP=%%~a
if "%FP%"=="" (echo FINGERPRINT READ FAILED - tell Hermes ^& pause ^& exit /b 2)
echo fingerprint=%FP%
echo the game - balance assist ON (auto-generated; build ed94f382 line 3c33feb9). Close the window to stop.
start "" "%OWNER_EXE%" -trial -duration 3600 -label scene_play -out "Trials\Runs" -sourceFingerprint %FP% -buildManifest "Build\Owner\build-manifest.json" -fixedDelta 0.005 -visual -shotRate 0.2 -autoGetUp 1.5 -poseGetUpReach 0.35 -poseHandGrip 1 -poseSwingT 0.600000 -poseStanceT 0.209341 -poseLift 0.064997 -poseLandShare 0.798491 -poseHeightGain 540.5688 -poseHeightDamp 11.72782 -posePlaceMargin 0.019678 -poseLeadSwap 0.075425 -poseStepMin -0.335024 -poseStepMax 0.326438 -poseArmSwing 7.742455 -poseAnkleP 0.554069 -poseAnkleD 0.855518 -poseTailHold 0.010 -poseAnkleCarry -14 -poseAnkleRamp 0.09 -poseBobAmp 0.02 -posePushDeliverP 12 -posePlaceKneeLive 1 -neckP 2.5 -assist 1 -assistGain 1.5 -assistCenter 4 -assistHoldK 30 -assistCenterFree 1 -clAnkleCap 0.6 -assistAnkleCrouch 0.5 -assistWalkDir 1 -assistBudget 100000 -assistFmax 0.5 -clStanceClose 1 -clWalkEntry 2 -assistSettleWalk 1 -assistBrake 0.08 -assistBrakeFloor 0.45 -clStanceCloseRearm 8 -clStandLp 80 -clStanceCloseSpeed 0.6 -clStanceCloseLoop 1 -clStanceCloseAim 0.07 -clStanceCloseSeat 0.005 -fallReflexUnstep 0 -clClosePullCom 0.03 -clClosePullLift 0 -clCloseDoneLoad 200 -clClosePullHome 0.10 -clCloseShiftHold 1.5 -clEntryReady 6 -clEntryReadyQuiet 0.5 -clEntryLoad 1 -clEntryRear 0.01 -clEntryReadyStagger 0.10 -clEntryMask 1 -clEntryStraight 25 -clReleaseRamp 2.0 -clReleaseRampCatch 0.95 -clReleaseRampClose 0.05 -clReleaseRampPress 0 -clReleaseRampPressSpan 1 -clStanceCloseMin 0.05 -clReleasePhase 1 -clReleasePhaseD 0.10 -clReleasePhaseW 0.15 -clReleasePhaseMax 1.75 -clReleasePhaseLandU 0.80 -clReleasePhaseHold 2  -walkPlanBrake 0 -armCrossR 1.0 -armSwingLpL 0.7 -armSwingLpR 2.3 -armSwingPeakDb 0 -poseWalkSettleHand 1 -poseWalkSettleExit 0.6 -poseWalkSettleExitQuiet 0.05 -assistSettleDamp 3 -assistSettleDampMax 0.15 -hipDynMode 1 -hipDynAmp 0.0105 -hipDynIn 0.05 -assistStandUp 0.10 -poseWalkSymTrim0 -0.060 -camAuto 1 -camAutoMargin 0.6 -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "%LOCALAPPDATA%\Temp\grok-burn\scene_play.log"
set LAUNCH_FP=%FP%
:poll
%SystemRoot%\System32\ping.exe -n 61 127.0.0.1 >nul
set NEW_FP=
for /f "tokens=2 delims=:, " %%a in ('findstr /c:"\"fingerprint\"" "Build\Headless\build-manifest.json"') do set NEW_FP=%%~a
if "%NEW_FP%"=="%LAUNCH_FP%" goto poll
wmic process where "Name='BalanceTrial.exe' and ExecutablePath like '%%\\Build\\Owner\\%%'" CALL terminate >nul 2>nul
goto watch
