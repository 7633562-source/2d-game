using System.Globalization;
using System.Text;
using UnityEngine;

// Tree stand metrics. Read-only, no forces.
public class PlantTrialRecorder
{
    private readonly PlantTree tree;
    private readonly TrialRunInfo info;
    private int samples;
    private double sumAbsJoint;
    private double sumJointSq;
    private float maxAbsJoint;
    private float startCrownY;
    private float minCrownY = float.MaxValue;
    private float lastAbsJoint;
    private float lastCrownY;
    private float settleAbsJoint;
    private bool settled;
    // Sway-never-topple metrics, same four keys as AppleTrialRecorder so the
    // oak and the apple stand are read by one probe script.
    private float maxTrunkLeanDeg;
    private float maxPinnedFraction;
    private float maxBaseSlideM;
    private float startCrownTipX;
    private float swayAmpM;
    private readonly float[] orderMaxAngle = new float[4];
    private readonly float[] orderLimitDeg = new float[4];
    private readonly float[] orderStiffness = new float[4];
    private readonly float[] orderGradient = new float[4];
    private readonly float[] orderBindDeg = new float[4];

    public PlantTrialRecorder(PlantTree tree, TrialRunInfo info)
    {
        this.tree = tree;
        this.info = info;
        if (tree != null)
            startCrownY = tree.GetCrownY();
    }

    public void Sample(float time)
    {
        if (tree == null) return;
        float abs = tree.GetMaxAbsJointAngle();
        float crown = tree.GetCrownY();
        if (samples == 0)
            startCrownY = crown;
        lastAbsJoint = abs;
        lastCrownY = crown;
        if (abs > maxAbsJoint)
            maxAbsJoint = abs;
        if (crown < minCrownY)
            minCrownY = crown;
        sumAbsJoint += abs;
        sumJointSq += abs * abs;
        // Sway-never-topple: stem lean, hinge pinning, base slide and the
        // crown tip's horizontal excursion over the whole run.
        float trunkLean = tree.GetTrunkLeanDeg();
        if (trunkLean > maxTrunkLeanDeg) maxTrunkLeanDeg = trunkLean;
        float pinned = tree.GetHingePinnedFraction();
        if (pinned > maxPinnedFraction) maxPinnedFraction = pinned;
        float baseSlide = tree.GetBaseSlideM();
        if (baseSlide > maxBaseSlideM) maxBaseSlideM = baseSlide;
        Vector2 crownTip = tree.GetCrownTip();
        if (samples == 0) startCrownTipX = crownTip.x;
        float amp = Mathf.Abs(crownTip.x - startCrownTipX);
        if (amp > swayAmpM) swayAmpM = amp;
        tree.SampleOrderMaxAngles(orderMaxAngle);
        if (!settled && time >= 2f)
        {
            settleAbsJoint = abs;
            settled = true;
        }
        samples++;
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        StringBuilder json = new StringBuilder(512);
        json.Append("{");
        AppendNum(json, "simulatedSeconds", simulatedSeconds, false);
        if (wallSeconds >= 0f)
            AppendNum(json, "wallPerSim", simulatedSeconds > 0f ? wallSeconds / simulatedSeconds : 0f);
        json.Append(",\"samples\":").Append(samples.ToString(CultureInfo.InvariantCulture));
        AppendMetrics(json);
        if (info != null)
        {
            AppendNum(json, "duration", info.Duration);
            AppendStr(json, "sourceFingerprint", info.SourceFingerprint);
        }
        json.Append("}");
        return json.ToString();
    }

    public void AppendMetrics(StringBuilder json)
    {
        float mean = samples > 0 ? (float)(sumAbsJoint / samples) : 0f;
        float rms = samples > 0 ? (float)System.Math.Sqrt(sumJointSq / samples) : 0f;
        float drop = startCrownY - minCrownY;
        AppendNum(json, "maxAbsJointAngle", maxAbsJoint);
        AppendNum(json, "meanAbsJointAngle", mean);
        AppendNum(json, "rmsJointAngle", rms);
        AppendNum(json, "settleAbsJointAngle", settleAbsJoint);
        AppendNum(json, "finalAbsJointAngle", lastAbsJoint);
        AppendNum(json, "crownDrop", drop);
        AppendNum(json, "startCrownY", startCrownY);
        AppendNum(json, "minCrownY", minCrownY < float.MaxValue ? minCrownY : 0f);
        AppendNum(json, "finalCrownY", lastCrownY);
        AppendNum(json, "maxTrunkLeanDeg", maxTrunkLeanDeg);
        AppendNum(json, "hingePinnedFraction", maxPinnedFraction);
        AppendNum(json, "baseSlideM", maxBaseSlideM);
        AppendNum(json, "swayAmpM", swayAmpM);
        if (tree != null)
        {
            // Ordered-stem design + per-order peak angle, so the next kind's
            // numbers are sized from a measurement rather than a guess.
            tree.GetOrderDesign(orderLimitDeg, orderStiffness, orderGradient, orderBindDeg);
            for (int o = 0; o < orderMaxAngle.Length; o++)
            {
                AppendNum(json, "treeOrder" + o + "MaxAngleDeg", orderMaxAngle[o]);
                AppendNum(json, "treeOrder" + o + "BindDeg", orderBindDeg[o]);
                AppendNum(json, "treeOrder" + o + "LimitDeg", orderLimitDeg[o]);
                AppendNum(json, "treeOrder" + o + "Stiffness", orderStiffness[o]);
                AppendNum(json, "treeOrder" + o + "GravityGradient", orderGradient[o]);
            }
        }
        if (tree != null)
        {
            json.Append(",\"treeSegmentCount\":").Append(tree.SegmentCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeDynamicCount\":").Append(tree.DynamicBodyCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeRigidbodyCount\":").Append(tree.RigidbodyCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeWoodColliderCount\":").Append(tree.WoodColliderCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treePerchPadCount\":").Append(tree.PerchPadCount.ToString(CultureInfo.InvariantCulture));
            AppendStr(json, "treeRig", tree.rig.ToString());
            AppendStr(json, "treeKind", tree.ResolvedKind.ToString());
            AppendStr(json, "treeSeason", tree.ResolvedSeason.ToString());
            json.Append(",\"treeSeed\":").Append(tree.seed.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeLeafCount\":").Append(tree.LeafCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeBloomCount\":").Append(tree.BloomCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeFruitCount\":").Append(tree.FruitCount.ToString(CultureInfo.InvariantCulture));
            // Lilac raceme / hazel catkin tally without AttachCluster cards.
            AppendNum(json, "treeLeafTintR", tree.LeafTintR);
            AppendNum(json, "treeLeafTintG", tree.LeafTintG);
            AppendNum(json, "treeLeafTintB", tree.LeafTintB);
            AppendNum(json, "treeLeafTintFarR", tree.LeafTintFarR);
            AppendNum(json, "treeLeafTintFarG", tree.LeafTintFarG);
            AppendNum(json, "treeLeafTintFarB", tree.LeafTintFarB);
            json.Append(",\"treeBarkPlateCount\":").Append(tree.BarkPlateCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeCrownMassCount\":").Append(tree.CrownMassCount.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "treeCrownSizeX", tree.CrownSizeX);
            AppendNum(json, "treeCrownSizeY", tree.CrownSizeY);
            json.Append(",\"treeRootFlareCount\":").Append(tree.RootFlareCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeForkCrotchCount\":").Append(tree.ForkCrotchCount.ToString(CultureInfo.InvariantCulture));
            // Join pad + 0.88 daughter fill. Not a 14 cm T-bar.
            // Pack-tip oak: one tuft on true tips. Internodal spurs at
            // depth 2 add clusters under the crown (not a second star).
            // SprayHub on pack-tip stays off.
            // Peak TipTip/TipRTip ride grow-time leaf sprites (willow
            // sickle, bush ovate, packed palmate/kite, garden cup).
            // treeLeafSpriteCount includes them. LateUpdate does not Find.
            json.Append(",\"treeLeafClusterCount\":").Append(tree.LeafClusterCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeLeafSpriteCount\":").Append(tree.LeafSpriteCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeLeafSwayCount\":").Append(tree.LeafSwayCount.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "treeLeafTilt", tree.LeafTiltDegrees);
            json.Append(",\"treeLeafPackTip\":").Append(tree.LeafPackTip ? "true" : "false");
            json.Append(",\"treeBranchSpurCount\":").Append(tree.BranchSpurCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeBudCount\":").Append(tree.BudCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeWinterWoodyCrown\":").Append(tree.WinterWoodyCrown ? "true" : "false");
            AppendNum(json, "treeWoodTwigR", tree.WoodTwigR);
            AppendNum(json, "treeWoodTwigG", tree.WoodTwigG);
            AppendNum(json, "treeWoodTwigB", tree.WoodTwigB);
            // Kind plates after season lerp. Winter orchard russet
            // must not stay a summer flake on a grey bole.
            AppendNum(json, "treeBarkLiteR", tree.BarkLiteR);
            AppendNum(json, "treeBarkLiteG", tree.BarkLiteG);
            AppendNum(json, "treeBarkLiteB", tree.BarkLiteB);
            json.Append(",\"treeGrowLeaves\":").Append(tree.growLeaves ? "true" : "false");
            // Orchard calendar. Spring/winter Apple fruitCount 0;
            // summer/autumn keep the crop. Oak stays false.
            json.Append(",\"treeGrowFruit\":").Append(tree.growFruit ? "true" : "false");
            json.Append(",\"treeHasLeafSway\":").Append(tree.HasLeafSway ? "true" : "false");
            json.Append(",\"treeHasLookSway\":").Append(tree.HasLookSway ? "true" : "false");
            AppendNum(json, "treeForkAngle", tree.forkAngle);
            AppendNum(json, "treeMaxAbsBranchHeading", tree.MaxAbsBranchHeading);
            AppendNum(json, "treeVisualTaper", tree.visualTaper);
            AppendNum(json, "treeWoodJoinScale", tree.WoodJoinScale);
            AppendNum(json, "treeWoodJoinInner", tree.WoodJoinScaleInner);
            AppendNum(json, "treeWoodTipLeader", tree.WoodTipScaleLeader);
            AppendNum(json, "treeWindSailTrunk", tree.WindSailTrunk);
            AppendNum(json, "treeWindSailTip", tree.WindSailTip);
            AppendNum(json, "treeWindSailSeason", tree.WindSailSeason);
            AppendNum(json, "treePictureSeasonSail", tree.PictureSeasonSail);
            AppendNum(json, "treeWindSailLean", tree.WindSailLean);
            // Oak tuft hub at 45°. PalmateTrack 0.70 flatten matches
            // palmate; 0.55 left leftover heading (flag on the stick).
            AppendNum(json, "treeLeafClusterRest", tree.LeafClusterRestDegrees);
            // Oak leader ~7.8° of fork 28. 8° deadzone left JSON at 0
            // (hat on the pole). SpreadLimbDeg 5 flattens PalmateTrack.
            AppendNum(json, "treeLeafClusterRestLeader", tree.LeafClusterRestLeader);
            AppendNum(json, "treeLeafClusterHangPad", tree.LeafClusterHangPad);
            AppendNum(json, "treeLeafLiveTrack", tree.LeafLiveTrack);
            AppendNum(json, "treeLeafCrownHangSail", tree.LeafCrownHangSail);
            // Nodal tuft + lilac raceme + hazel catkin. Wood heading
            // ~8° of fork 28 left JSON at pole 1 when paint skipped.
            // Rachis / Calyx / ForkFill / spray laterals ride the pad;
            // plum Knot / Suture / Notch and hazel CatkinTail too.
            // Poplar/ovate Waist and pine Needle1 ride KiteL / Base /
            // Sheath so a 0.28 box does not fill the sinus.
            // Needle1 / ovate Base / apple cheeks / bloom petals open
            // the remaining bite after Sheath / KiteR left.
            // Pine Body / willow Hang4 / cedar FanBody fill the sky
            // slit after hem pads left the mast; Needle2 rides Sheath;
            // hazel catkin packets open like lilac florets.
            // Fill / CapFill / Hang5 / FanFill then sat ON that mast
            // (cross x=0). Left/right lips close it; poplar kite too.
            // ForkFill / Collar / Flake2 / Rime / spray Lance then
            // still belted the daughter and the winter cane. Flanks
            // only; hang sail still Notes the hub plate.
            // Body/FanBody/Cap then kissed Fill at x=0 and rebuilt
            // a 0.32-span mast. Main pads close the cone; Fill chips
            // ride those pads (outer < 0). Pine fascicle fans right.
            // Willow Hang5/Hang6, cedar FanDome/FanDomeR, and bush
            // TuftL/TuftR kiss at x=0 the same way (not a Fill mast).
            // Apple/hazel ovate CheekR, winter TwigForkR/DomeR/FR,
            // bloom PetalL/R, garden 0° PetalR, berry PetalR kiss.
            // Poplar KiteL2/KiteR2 kiss the flame hem; internodal
            // spur cone + spreadLimb 5°; spring treeLeafTint* walks
            // kind. Oak palmate sinus stays empty.
            // After FanBody kiss: pine Body still slit vs BodyR;
            // autumn hang pad / tilt / sail still summer at 1x. Now
            // Body kisses BodyR; hang 0.50; tilt +18; sail 1.18.
            // Oak palmate sinus stays empty.
            // After artist hem 16:50: poplar Apex/leaflet Tip and
            // orchard caudal were still one-tents; winter
            // weep/flame/coppice/orchard skipped TwigDomeR; bloom
            // PetalTip left-only. ApexR/TipR/SkirtR/TwigDomeR/
            // PetalTipR. Oak palmate sinus stays empty.
            // After artist far 17:05: oak/poplar Wrist, ovate
            // Petiole, bush Tooth, oak/orchard Core, orchard near
            // Skirt, winter Bud, garden Blade/Midrib still
            // left-only. WristR/PetioleR/ToothR/CoreR/Skirt;
            // BudR; BladeR. Fork 28 / taper 0.58 stay.
            // After artist WaistR 17:22: ForkWeb/Fill still a T-bar
            // at 28°; oak branch Crack/Flake no CorkTip; winter
            // Frost right-only. ForkWebLip/ForkFillLip;
            // CrackTip/FlakeTip; FrostL. Fork 28 / taper 0.58 stay.
            // After artist CollarTip 17:35: stump RootSoil/Butt
            // still a plank; winter TrunkRime/FrostCap a table;
            // cane Frost a rectangle. RootSoilTip/RootButtTip;
            // RimeTip/FrostCapTip; FrostTip. Fork 28 / taper 0.58 stay.
            // After artist ConeLTip 17:56: packed/crown no LobeL1;
            // pine Cap/Fringe posts; cedar FanL2/Fringe coins;
            // garden petal/leaflet cards. LobeL1; CapTip/FringeTip;
            // FanL2Tip; PetalTip/LeafletTip. Fork 28 / taper 0.58 stay.
            // After artist CarmBTip 18:16: inner LobeL0/caudal LobeL2
            // still blunt after LobeL1Tip; pine Body a plank after
            // SlopeL2Tip; plum Drop / apple Cheek coins; Calyx a bark
            // card; garden Spent a hip; berry RunnerLeaf a card.
            // LobeL0Tip/LobeL2Tip/WristTip; packed Lobe0Tip; crown
            // LobeLTip/SkirtTip; BodyTip; DropTip/CheekLTip;
            // CalyxTip; SpentTip; RunnerLeafTip. Fork 28 / taper 0.58 stay.
            // After artist ConeCoreTip 18:37: poplar KiteL2 a blunt
            // hem; packed kite a paddle; bush Tuft/Dome a sofa;
            // hazel Tuft0 a card; winter TwigDome a flag; oak CoreR
            // blunt; Taper stairs; garden Floret/Midrib/Petiole
            // cards; berry MidWaist/RunnerLeaf2/SpringBloom cards.
            // KiteL2Tip; packed KiteLTip; bush TuftLTip; TwigDomeTip;
            // TaperTip; FloretTip/PetioleTip; RunnerLeaf2Tip.
            // Fork 28 / taper 0.58 stay.
            // After artist WaistTip 18:52: poplar Base a hem card;
            // packed ovate Base/CheekR a spur egg; spray CheekR a
            // paddle; orchard CoreR blunt; ovate Base/CheekR coins;
            // internodal Cone a telegraph; garden SpentR a hip coin;
            // berry SpringBloom a flush card. BaseTip/BaseRTip;
            // packed BaseTip/CheekRTip; spray CheekRTip; CoreRTip;
            // ovate BaseTip/CheekRTip; ConeTip; SpentRTip;
            // SpringBloom parent off. Fork 28 / taper 0.58 stay.
            // After artist ForkFillTip 19:32: crown/tuft still used 8°
            // while Grow/spurs use 5°. Oak leader 7.8° of fork 28 stayed
            // a hat on the pole. SpreadLimbDeg; autumn hang pad 0.40;
            // internodal ConeTip on the short core. Fork 28 / taper
            // 0.58 stay.
            // After Visual 19:46 Fan2: FanR still crossed the cedar
            // spine; pine BodyR kissed the mast; willow Tip a curtain;
            // autumn +10 / hang 0.40 still summer. FanR outer; BodyR
            // off-spine; Tip sickle; leafTilt +16 / hang 0.46.
            // Fork 28 / taper 0.58 stay.
            AppendNum(json, "treeLeafClusterHangSail", tree.LeafClusterHangSail);
            json.Append(",\"treeLeafSwayWorldGust\":").Append(tree.LeafSwayWorldGust ? "true" : "false");
            AppendNum(json, "treeLeafSwayDegrees", tree.leafSwayDegrees);
            AppendNum(json, "treeWindSpeed", tree.windSpeed);
            AppendNum(json, "treeWindScale", tree.windScale);
            AppendNum(json, "treeWindGust", tree.windGust);
            AppendNum(json, "treeWindPeriod", tree.windPeriod);
            // Injected sitter load: a run that bends the crown must say which
            // load bent it, or the number is not evidence.
            AppendNum(json, "treePerchLoadKg", tree.perchLoadKg);
            json.Append(",\"treePerchLoadSlot\":").Append(tree.perchLoadSlot.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "treePerchLoadUntil", tree.perchLoadUntilSeconds);
            tree.GetHoldStats(out float meanHold, out float maxHold);
            AppendNum(json, "meanAbsHoldTorque", meanHold);
            AppendNum(json, "maxAbsHoldTorque", maxHold);
        }
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("0.########", CultureInfo.InvariantCulture));
    }

    private static void AppendStr(StringBuilder json, string key, string value)
    {
        json.Append(",\"").Append(key).Append("\":\"");
        if (!string.IsNullOrEmpty(value))
            json.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        json.Append('"');
    }
}
