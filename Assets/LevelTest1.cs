using System.Collections.Generic;
using UnityEngine;

// First authored yard. Continuous support, 5 cm curbs only — not a wall.
// GPT Sol: climb, dip, square, gate, finish, lookout; curb faces; back fence.
public static class LevelTest1
{
    public const string Title = "Yard";
    public const float SignX = 48f;
    public const float EndX = 96f;
    // Level1 drawn tableau: player left of the apple. World showcase stays 0.
    // LevelGenerator.ApplyOverrideFlag copies playerSpawnX here before spawn.
    public const float DrawnPlayerX = -8f;
    public static float PlayerX = 0f;

    public struct Slab
    {
        public float left;
        public float width;
        public float surfaceY;
        public string name;
    }

    // High back-fence rails. Picture perch only — not a collider.
    public struct FenceRail
    {
        public float x;
        public float width;
        public float topY;
    }

    // Line between post tops. Picture only — not a collider.
    public struct Clothesline
    {
        public float x0;
        public float x1;
        public float y;
    }

    // Loaf perch. Picture only — not Dog, not a collider.
    public struct CatPerch
    {
        public float x;
        public float y;
        public float face;
    }

    // Hang peg. Picture only — not a Light2D, not a collider.
    public struct LanternPeg
    {
        public float x;
        public float y;
        public float hang;
    }

    // Hinge on the west gate post. Picture leaf only — not a collider.
    public struct GateHinge
    {
        public float x;
        public float y;
        public float width;
        public float height;
        public float restZ;
    }

    // Cup on a high rail. Picture only — not Bird, not a collider.
    public struct NestPeg
    {
        public float x;
        public float y;
        public float face;
    }

    // Terracotta on the slab. Picture only — not PlantFlower, not a collider.
    public struct FlowerPotPeg
    {
        public float x;
        public float face;
    }

    // Box on a post. Picture only — not Bird, not a collider.
    public struct BirdhousePeg
    {
        public float x;
        public float y;
        public float face;
        public bool stake;
    }

    // Pedestal on the slab. Picture only — not Bird, not a puddle, not a collider.
    public struct BirdBathPeg
    {
        public float x;
        public float face;
    }

    // Loaf on the slab / puddle rim. Picture only — not Insect, not Bird.
    public struct FrogPeg
    {
        public float x;
        public float face;
    }

    // Hover over a bowl or grate. Picture only — not Insect, not Bird.
    public struct DragonflyPeg
    {
        public float x;
        public float face;
    }

    // Bask on post lumber. Picture only — not Insect, not Bird, not Dog.
    public struct LizardPeg
    {
        public float x;
        public float y;
        public float face;
    }

    // Sit the dry grass. Picture only — not Insect, not Bird.
    public struct GrasshopperPeg
    {
        public float x;
        public float face;
    }

    // Crawl the bare dirt. Picture only — not Insect, not Bird.
    public struct AntPeg
    {
        public float x;
        public float face;
    }

    // Sit the dry grass after dusk. Picture only — not Insect, not Bird.
    public struct CricketPeg
    {
        public float x;
        public float face;
    }

    // Crawl the bare dirt after dusk. Picture only — not Insect, not Bird.
    public struct WormPeg
    {
        public float x;
        public float face;
    }

    // Skate the standing puddles by day. Picture only — not Insect, not Bird.
    public struct StriderPeg
    {
        public float x;
        public float face;
    }

    // Circle a hanging lantern after dusk. Picture only — not Bird, not Insect.
    public struct BatPeg
    {
        public float x;
        public float y;
        public float hang;
        public float face;
    }

    // Sit the bay between the high and low back-fence rails. Picture only —
    // not Insect, not Bird, not a collider.
    public struct WebPeg
    {
        public float x;
        public float y;
        public float width;
        public float height;
        public float face;
    }

    // Dart the night slab under a loafing cat or at fire crumbs. Picture
    // only — not Dog, not Insect, not Hedgehog, not a collider.
    public struct MousePeg
    {
        public float x;
        public float face;
    }

    // Sit the day grass. Picture only — not Dog, not Insect, not Hedgehog,
    // not a collider. Mice own the night slab; hoppers own the insect hop.
    public struct RabbitPeg
    {
        public float x;
        public float face;
    }

    // Sit a birdhouse roof / short post by day. Picture only — not Dog,
    // not Insect, not Bird, not a collider. Rabbits own the grass;
    // lizards own the lumber; house sitters own the hole after dusk.
    public struct SquirrelPeg
    {
        public float x;
        public float y;
        public float face;
    }

    // Sit a lantern post cap after dusk. Picture only — not Bird, not
    // Insect, not Dog, not a collider. Squirrels own the house roofs by
    // day; bats hunt the hanging bowl, so the cap was empty.
    public struct OwlPeg
    {
        public float x;
        public float y;
        public float face;
    }

    // Wash the night slab at firewood / berry crumbs. Picture only — not
    // Dog, not Hedgehog, not Insect, not a collider. Mice dart the thin
    // crumb line; moths own the flame air, so the log flank was empty.
    public struct RaccoonPeg
    {
        public float x;
        public float face;
    }

    // Bask the puddle bank by day. Picture only — not Insect, not Bird,
    // not Dog, not a collider. Frogs own the dry rim hop; striders own
    // the film, so the mud bank was empty. Raccoons own the night logs.
    public struct TurtlePeg
    {
        public float x;
        public float face;
    }

    // Loaf on the water's edge. Picture only — not Bird, not Insect.
    public struct DuckPeg
    {
        public float x;
        public float face;
    }

    // Wade the puddle film by day. Picture only — not Bird, not Insect.
    public struct HeronPeg
    {
        public float x;
        public float face;
    }

    // Dart under the puddle film by day. Picture only — not Insect, not
    // Bird. Herons spear empty water; striders own the surface; ducks
    // loaf the edge, so the OPEN water still had no prey.
    public struct MinnowPeg
    {
        public float x;
        public float face;
    }

    // Stand the wet bank. Picture only — not PlantFlower, not
    // TreeRig.Sway. Minnows dart the film; ducks loaf the edge;
    // turtles own the mud, so the MARGIN still had no reed.
    public struct CattailPeg
    {
        public float x;
        public float face;
    }

    // Sit on the puddle film. Picture only — not PlantFlower, not
    // a puddle collider. Cattails own the bank; minnows dart under;
    // striders skate the film, so the OPEN ellipse still had no plant.
    public struct LilyPadPeg
    {
        public float x;
        public float face;
    }

    // Stand the dry grass. Picture only — not PlantFlower, not
    // TreeRig.Sway. Cattails own the wet bank; lily pads the film;
    // pots own terracotta, so the DRY blades still had no wildflower.
    public struct DandelionPeg
    {
        public float x;
        public float face;
    }

    // Sit the damp shade. Picture only — not PlantFlower, not
    // Insect. Cattails own the wet bank; lily pads the film;
    // dandelions the dry blades, so the DAMP MARGIN still had no
    // toadstool (grass skips IsWetAt, baths own the bowl).
    public struct MushroomPeg
    {
        public float x;
        public float face;
    }

    // Climb the post lumber. Picture only — not PlantFlower, not
    // TreeRig.Sway. Lizards bask the face; lanterns hang; houses
    // sit, so the WOOD itself still had no vine.
    public struct IvyPeg
    {
        public float x;
        public float face;
    }

    // Sit the shade at a fence foot or grove. Picture only — not
    // PlantFlower, not TreeRig.Sway. Ivy climbs the post; mushrooms
    // own the damp margin; dandelions the dry blades, so the SHADE
    // understory still had no frond.
    public struct FernPeg
    {
        public float x;
        public float face;
    }

    // Sit the dry lawn. Picture only — not PlantFlower, not
    // TreeRig.Sway. Dandelions own the tall clocks; ferns the shade
    // foot; grass the blades, so the LOW DRY carpet still had no
    // trefoil.
    public struct CloverPeg
    {
        public float x;
        public float face;
    }

    // Sit the dry waste verge. Picture only — not PlantFlower, not
    // TreeRig.Sway. Clover owns the low carpet; dandelions the
    // clocks, so the DISTURBED edge still had no spine.
    public struct ThistlePeg
    {
        public float x;
        public float face;
    }

    // Sit the trodden path edge. Picture only — not PlantFlower, not
    // TreeRig.Sway. Clover owns the lawn; thistles the waste verge;
    // ants the bare dirt crawl, so the WALKED edge still had no
    // ribwort spike.
    public struct PlantainPeg
    {
        public float x;
        public float face;
    }

    // Sit the damp fence-foot waste. Picture only — not PlantFlower,
    // not TreeRig.Sway. Thistles own the dry verge; ferns the shade
    // foot; plantain the walked edge; ivy the post climb, so the
    // NITROGEN patch at a fence or grove drip still had no sting.
    public struct NettlePeg
    {
        public float x;
        public float face;
    }

    // Sit the open disturbed verge. Picture only — not PlantFlower,
    // not TreeRig.Sway. Nettles own the fence-foot sting; plantain
    // the trodden spike; thistles the dry waste; cattails the wet
    // reed, so the OPEN nitrogen (path, bath bank, square) still
    // had no rusty panicle.
    public struct DockPeg
    {
        public float x;
        public float face;
    }

    // Arch the fence-bay hedge. Picture only — not PlantFlower,
    // not TreeRig.Sway. Ivy climbs the post; docks own the rusty
    // panicle; nettles the fence-foot sting; thistles the dry
    // globe, so the BAY between posts still had no arching cane.
    public struct BramblePeg
    {
        public float x;
        public float face;
    }

    // Stand the sunny garden column. Picture only — not PlantFlower,
    // not TreeRig.Sway. Brambles arch the bay; ivy climbs the post;
    // docks the rusty panicle; terracotta pots own the clay bloom,
    // so the TALL cultivated spike against a bed / bench still had
    // no hollyhock.
    public struct HollyhockPeg
    {
        public float x;
        public float face;
    }

    // Sit the sunny garden shrub. Picture only — not PlantFlower,
    // not TreeRig.Sway. Hollyhocks own the tall column; strawberries
    // the low fruit; terracotta the clay bloom; brambles the wild
    // arch, so the ROUND mid cultivated bush against a bed / apple
    // / bench still had no rose.
    public struct RosePeg
    {
        public float x;
        public float face;
    }

    // Sit the sunny herb border. Picture only — not PlantFlower,
    // not TreeRig.Sway. Hollyhocks own the tall column; roses the
    // round woody shrub; strawberries the low fruit; terracotta
    // the clay bloom; docks the rusty panicle, so the LOW grey-
    // purple cultivated mound against a bed / walk / square still
    // had no lavender.
    public struct LavenderPeg
    {
        public float x;
        public float face;
    }

    // Stand the sunny garden fan. Picture only — not PlantFlower,
    // not TreeRig.Sway. Hollyhocks own the tall column; roses the
    // round woody shrub; lavender the low herb mound; cattails
    // the wet sausage; ferns the shade pinnae, so the MID sword-
    // leaf flag against a bed / walk / square still had no iris.
    public struct IrisPeg
    {
        public float x;
        public float face;
    }

    // Stand the sunny garden cup. Picture only — not PlantFlower,
    // not TreeRig.Sway. Hollyhocks own the tall column; roses the
    // round woody shrub; lavender the low herb mound; irises the
    // sword-leaf flag, so the CUP-ON-A-STEM bloom against a bed /
    // walk / square still had no tulip.
    public struct TulipPeg
    {
        public float x;
        public float face;
    }

    // Stand the sunny garden trumpet. Picture only — not PlantFlower,
    // not TreeRig.Sway. Hollyhocks own the tall column; roses the
    // round woody shrub; lavender the low herb mound; irises the
    // sword-leaf flag; tulips the cup-on-a-stem, so the TRUMPET
    // corona against a bed / walk / square still had no daffodil.
    public struct DaffodilPeg
    {
        public float x;
        public float face;
    }

    // Sit the sunny garden disk. Picture only — not PlantFlower,
    // not TreeRig.Sway. Hollyhocks own the tall column; roses the
    // round woody shrub; lavender the low herb mound; irises the
    // sword-leaf flag; tulips the cup-on-a-stem; daffodils the
    // trumpet corona; dandelions the wild clock, so the RAY-DISK
    // bloom against a bed / walk / square still had no daisy.
    public struct DaisyPeg
    {
        public float x;
        public float face;
    }

    // Sit the sunny garden bowl. Picture only — not PlantFlower,
    // not TreeRig.Sway. Hollyhocks own the tall column; roses the
    // round woody shrub; lavender the low herb mound; irises the
    // sword-leaf flag; tulips the closed cup; daffodils the
    // trumpet; daisies the ray-disk; dandelions the wild clock,
    // so the PAPER BOWL of four crumpled petals and a dark blotch
    // against a bed / walk / square still had no poppy.
    public struct PoppyPeg
    {
        public float x;
        public float face;
    }

    // Stand the sunny garden star. Picture only — not PlantFlower,
    // not TreeRig.Sway. Hollyhocks own the tall column; roses the
    // round woody shrub; lavender the low herb mound; irises the
    // sword-leaf flag; tulips the closed cup; daffodils the
    // trumpet; daisies the ray-disk; poppies the paper bowl;
    // lily pads the puddle film, so the SIX-POINT flare of
    // pointed tepals and a spotted throat against a bed / walk /
    // square still had no lily.
    public struct LilyPeg
    {
        public float x;
        public float face;
    }

    // Stand the sunny garden disk-on-a-stalk. Picture only — not
    // PlantFlower, not TreeRig.Sway. Hollyhocks own the tall column;
    // roses the round woody shrub; lavender the low herb mound;
    // irises the sword-leaf flag; tulips the closed cup; daffodils
    // the trumpet; daisies the small ray-disk; poppies the paper
    // bowl; lilies the six-point flare; dandelions the wild clock,
    // so the GIANT yellow head with a dark seed disk on a thick
    // stalk against a bed / walk / square still had no sunflower.
    public struct SunflowerPeg
    {
        public float x;
        public float face;
    }

    // Sit the damp shade cushion. Picture only — not PlantFlower,
    // not TreeRig.Sway. Hollyhocks own the tall column; roses the
    // round woody shrub; lavender the grey-purple spike mound;
    // irises the sword-leaf flag; tulips the closed cup; daffodils
    // the trumpet; daisies the small ray-disk; poppies the paper
    // bowl; lilies the six-point flare; sunflowers the giant
    // disk-on-a-stalk, so the LOW five-petal FACE with a yellow
    // throat and heart leaves against a mushroom / fern foot
    // still had no violet.
    public struct VioletPeg
    {
        public float x;
        public float face;
    }

    // Sit the cool lawn goblet. Picture only — not PlantFlower,
    // not TreeRig.Sway. Tulips own the closed cup on a tall scape;
    // violets the five-petal face with heart leaves; irises the
    // sword flag, so a SHORT open GOBLET with grass blades and
    // orange stamens on cool turf still had no crocus.
    public struct CrocusPeg
    {
        public float x;
        public float face;
    }

    // Sit the damp woodland hanging bell. Picture only — not
    // PlantFlower, not TreeRig.Sway. Crocuses own the short goblet;
    // violets the five-petal face; lavender the upright spike, so a
    // HANGING cluster of bells on an arching scape against a wet
    // bank / fence-foot / grove drip still had no bluebell.
    public struct BluebellPeg
    {
        public float x;
        public float face;
    }

    // Sit the staked kitchen vine. Picture only — not PlantFlower,
    // not TreeRig.Sway, not PlantStrawberry. Bluebells own the
    // hanging woodland bell; hollyhocks the bloom column;
    // sunflowers the disk-on-a-stalk; strawberries the low edible
    // bush, so a STAKE with climbing compound leaves and hanging
    // red fruit still had no tomato.
    public struct TomatoPeg
    {
        public float x;
        public float face;
    }

    // Sit the round kitchen head. Picture only — not PlantFlower,
    // not TreeRig.Sway, not PlantStrawberry. Tomatoes own the
    // staked vine; sunflowers the disk-on-a-stalk; hollyhocks the
    // bloom column; mushrooms the toadstool cap, so a WRAPPER of
    // overlapping leaves sitting on the dirt still had no cabbage.
    public struct CabbagePeg
    {
        public float x;
        public float face;
    }

    // Sit the ribbed kitchen gourd. Picture only — not PlantFlower,
    // not TreeRig.Sway, not PlantStrawberry. Cabbages own the
    // round wrapper; tomatoes the staked vine; sunflowers the
    // disk-on-a-stalk; mushrooms the toadstool cap, so a SQUAT
    // ribbed fruit on a trailing dirt vine still had no pumpkin.
    public struct PumpkinPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen tassel. Picture only — not PlantFlower,
    // not TreeRig.Sway, not PlantStrawberry. Pumpkins own the
    // ribbed gourd; cabbages the round wrapper; tomatoes the
    // staked vine; sunflowers the disk-on-a-stalk; cattails the
    // brown sausage, so a TALL stalk with strap leaves, a husked
    // ear, and a wispy tassel still had no corn.
    public struct CornPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen root. Picture only — not PlantFlower,
    // not TreeRig.Sway, not PlantStrawberry. Corn owns the
    // tall tassel; pumpkins the ribbed gourd; cabbages the
    // round wrapper; tomatoes the staked vine; ferns the
    // shade pinna, so a TAPERING orange root in dirt with
    // ferny greens still had no carrot.
    public struct CarrotPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen spear. Picture only — not PlantFlower,
    // not TreeRig.Sway, not PlantStrawberry. Carrots own the
    // tapering root; corn the tall tassel; tomatoes the staked
    // vine; cattails the brown sausage, so a BLANCHED cylindrical
    // shaft with a fan of upright strap leaves still had no leek.
    public struct LeekPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen pole with hanging pods. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Leeks own the blanched spear; tomatoes the staked vine
    // with round red fruit; corn the tall tassel; cattails the
    // brown sausage, so a POLE with twining trifoliate leaves
    // and long hanging pods still had no bean.
    public struct BeanPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen globe in dirt. Picture only — not
    // PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Beans own the pole with hanging pods; leeks the
    // blanched spear; carrots the tapering cone, so a
    // ROUND papery bulb sitting in the dirt with hollow
    // tubular greens still had no onion.
    public struct OnionPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen clove cluster in dirt. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Onions own the round papery globe; leeks the blanched
    // spear; beans the pole with hanging pods, so a CLUSTER
    // of cloves under a papery sack still had no garlic.
    public struct GarlicPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen radish in dirt. Picture only — not
    // PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Garlic owns the clove cluster; onions the round papery
    // globe; carrots the tapering cone, so a SMALL round RED
    // root with a white tip and lobed greens still had no
    // radish.
    public struct RadishPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen potato in dirt. Picture only — not
    // PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Radishes own the small round red root; onions the
    // papery globe; carrots the tapering cone, so an
    // IRREGULAR brown tuber with eyes and pinnate leaflets
    // still had no potato.
    public struct PotatoPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen beet in dirt. Picture only — not
    // PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Potatoes own the irregular brown tuber; radishes
    // the small round red root; onions the papery globe,
    // so a ROUND purple root with RED-VEINED greens still
    // had no beet.
    public struct BeetPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen parsnip in dirt. Picture only — not
    // PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Beets own the round purple crown; carrots the orange
    // taper; potatoes the irregular brown tuber, so a
    // CREAMY wide cone with celery-like pinnate greens
    // still had no parsnip.
    public struct ParsnipPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen turnip in dirt. Picture only — not
    // PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Parsnips own the creamy wide cone; beets the round
    // purple crown; carrots the orange taper, so a ROUND
    // WHITE globe with a PURPLE shoulder and lobed greens
    // still had no turnip.
    public struct TurnipPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen rutabaga in dirt. Picture only — not
    // PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Turnips own the round white globe; parsnips the
    // creamy wide cone; beets the round purple crown, so
    // a LARGER YELLOW globe with a PURPLE cap and
    // glaucous collard greens still had no rutabaga.
    public struct RutabagaPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen kohlrabi ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Rutabagas own the larger yellow globe in dirt; turnips
    // the round white globe; beets the round purple crown, so
    // a PALE-GREEN swollen STEM sitting ON the slab with long
    // petioles from the crown still had no kohlrabi.
    public struct KohlrabiPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen celery ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Kohlrabis own the pale-green swollen STEM; leeks the
    // blanched SPEAR; cabbages the round WRAPPER, so RIBBED
    // upright stalks sitting ON the slab with compound
    // leaflets at the crown still had no celery.
    public struct CeleryPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen fennel ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Celeries own the RIBBED upright stalks; leeks the
    // blanched SPEAR; kohlrabis the pale-green swollen
    // STEM, so a SWOLLEN PALE bulb sitting ON the slab
    // with a dill-like feathery crown still had no fennel.
    public struct FennelPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen asparagus ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Fennels own the SWOLLEN PALE bulb; leeks the
    // blanched SPEAR; celeries the RIBBED upright stalks,
    // so THIN GREEN spears from a SCALY crown sitting ON
    // the slab still had no asparagus.
    public struct AsparagusPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen rhubarb ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Asparagus owns the THIN GREEN spears; leeks the
    // blanched SPEAR; fennels the SWOLLEN PALE bulb, so
    // THICK RED stalks with a HUGE wrinkled leaf sitting
    // ON the slab still had no rhubarb.
    public struct RhubarbPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen kale ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Rhubarb owns the THICK RED stalks; cabbage the ROUND
    // WRAPPER head; asparagus the THIN GREEN spears, so
    // CORRUGATED BLUE-GREEN standing leaves in a ROSETTE
    // sitting ON the slab still had no kale.
    public struct KalePeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen chard ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Kale owns the CORRUGATED BLUE-GREEN rosette; rhubarb
    // the THICK RED stalks; cabbage the ROUND WRAPPER, so
    // BRIGHT stalks with a LOBED leaf sitting ON the slab
    // still had no chard.
    public struct ChardPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen spinach ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Chard owns the BRIGHT stalks with a LOBED leaf; kale
    // the CORRUGATED BLUE-GREEN standing rosette; cabbage
    // the ROUND WRAPPER, so SMOOTH DARK-GREEN leaves in a
    // LOW rosette sitting ON the slab still had no spinach.
    public struct SpinachPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen lettuce ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Spinach owns the SMOOTH DARK-GREEN LOW rosette;
    // cabbage the TIGHT ROUND WRAPPER; kale the
    // CORRUGATED BLUE-GREEN standing rosette, so a LOOSE
    // PALE-GREEN head sitting ON the slab still had no
    // lettuce.
    public struct LettucePeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen broccoli ON the slab. Picture only —
    // not PlantFlower, not TreeRig.Sway, not PlantStrawberry.
    // Lettuce owns the LOOSE PALE-GREEN cup; cabbage the
    // TIGHT ROUND WRAPPER; spinach the SMOOTH DARK-GREEN
    // LOW rosette, so a TIGHT GREEN floret head on a
    // THICK stem sitting ON the slab still had no
    // broccoli.
    public struct BroccoliPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen cauliflower ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Broccoli owns the TIGHT GREEN
    // floret; cabbage the TIGHT ROUND WRAPPER; lettuce
    // the LOOSE PALE-GREEN cup, so a TIGHT CREAMY-WHITE
    // curd on a THICK stem sitting ON the slab still
    // had no cauliflower.
    public struct CauliflowerPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen Brussels sprout ON the slab.
    // Picture only — not PlantFlower, not TreeRig.Sway,
    // not PlantStrawberry. Broccoli owns the TIGHT GREEN
    // floret dome; cauliflower the TIGHT CREAMY-WHITE
    // curd; cabbage the TIGHT ROUND WRAPPER, so TIGHT
    // GREEN sprouts along a TALL stem sitting ON the
    // slab still had no Brussels.
    public struct BrusselsPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen artichoke ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Broccoli owns the TIGHT GREEN
    // floret dome; cauliflower the TIGHT CREAMY-WHITE
    // curd; Brussels the TIGHT GREEN sprouts along a
    // TALL stem; thistle the PURPLE bloom on the dry
    // verge, so a SCALY edible bud on a THICK stem
    // sitting ON the slab still had no artichoke.
    public struct ArtichokePeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen eggplant ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Tomato owns the ROUND RED ball
    // on a stake vine; artichoke the SCALY edible bud
    // on a THICK stem; thistle the PURPLE bloom on the
    // dry verge, so a GLOSSY PURPLE teardrop fruit on
    // a stem sitting ON the slab still had no eggplant.
    public struct EggplantPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen pepper ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Eggplant owns the GLOSSY
    // PURPLE teardrop hanging from a calyx; tomato
    // the ROUND RED ball on a stake vine; artichoke
    // the SCALY edible bud on a THICK stem, so a
    // GLOSSY BLOCKY pod sitting ON the slab still had
    // no pepper.
    public struct PepperPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen cucumber ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Pepper owns the GLOSSY BLOCKY
    // pod under a calyx; tomato the ROUND RED ball on
    // a stake vine; eggplant the GLOSSY PURPLE hanging
    // teardrop, so a LONG GREEN fruit on a vine sitting
    // ON the slab still had no cucumber.
    public struct CucumberPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen melon ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Cucumber owns the LONG GREEN
    // fruit along a vine; pumpkin the RIBBED gourd
    // on a dirt vine; pepper the GLOSSY BLOCKY pod
    // under a calyx, so a NETTED ROUND fruit on a
    // vine sitting ON the slab still had no melon.
    public struct MelonPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen watermelon ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Melon owns the NETTED ROUND
    // fruit on a vine; cucumber the LONG GREEN fruit
    // along a vine; pumpkin the RIBBED gourd on a
    // dirt vine, so a STRIPED OVAL fruit on a vine
    // sitting ON the slab still had no watermelon.
    public struct WatermelonPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen zucchini ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Watermelon owns the STRIPED
    // OVAL fruit on a vine; melon the NETTED ROUND
    // fruit; pumpkin the RIBBED gourd on a dirt
    // vine, so a YELLOW HOOK fruit on a vine sitting
    // ON the slab still had no zucchini.
    public struct ZucchiniPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen pattypan ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Zucchini owns the YELLOW HOOK
    // fruit on a vine; watermelon the STRIPED OVAL;
    // pumpkin the RIBBED gourd on a dirt vine, so a
    // YELLOW SCALLOPED DISK on a vine sitting ON the
    // slab still had no pattypan.
    public struct PattypanPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen butternut ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Pattypan owns the YELLOW
    // SCALLOPED DISK on a vine; zucchini the YELLOW
    // HOOK; pumpkin the RIBBED gourd on a dirt vine,
    // so a BEIGE BOTTLE fruit on a vine sitting ON
    // the slab still had no butternut.
    public struct ButternutPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen acorn ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway, not
    // PlantStrawberry. Butternut owns the BEIGE
    // BOTTLE fruit on a vine; pattypan the YELLOW
    // SCALLOPED DISK; pumpkin the RIBBED orange
    // gourd on a dirt vine, so a DARK-GREEN RIBBED
    // ACORN fruit on a vine sitting ON the slab
    // still had no acorn squash.
    public struct AcornPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen spaghetti squash ON the slab.
    // Picture only — not PlantFlower, not TreeRig.Sway,
    // not PlantStrawberry. Acorn owns the DARK-GREEN
    // RIBBED ACORN fruit on a vine; butternut the BEIGE
    // BOTTLE; pumpkin the RIBBED orange gourd on a dirt
    // vine, so a PALE-YELLOW OBLONG fruit on a vine
    // sitting ON the slab still had no spaghetti squash.
    public struct SpaghettiPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen delicata ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway,
    // not PlantStrawberry. Spaghetti owns the
    // PALE-YELLOW OBLONG fruit on a vine; acorn the
    // DARK-GREEN RIBBED ACORN; butternut the BEIGE
    // BOTTLE, so a CREAM OBLONG with DARK-GREEN
    // STRIPES on a vine sitting ON the slab still
    // had no delicata squash.
    public struct DelicataPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen carnival squash ON the slab.
    // Picture only — not PlantFlower, not TreeRig.Sway,
    // not PlantStrawberry. Delicata owns the CREAM
    // OBLONG with DARK-GREEN STRIPES on a vine;
    // spaghetti the PALE-YELLOW OBLONG; acorn the
    // DARK-GREEN RIBBED ACORN, so a ROUND CREAM
    // globe with ORANGE-GREEN MOTTLE on a vine
    // sitting ON the slab still had no carnival
    // squash.
    public struct CarnivalPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen sweet dumpling ON the slab.
    // Picture only — not PlantFlower, not TreeRig.Sway,
    // not PlantStrawberry. Carnival owns the ROUND
    // CREAM globe with ORANGE-GREEN MOTTLE on a vine;
    // delicata the CREAM OBLONG with DARK-GREEN
    // STRIPES; spaghetti the PALE-YELLOW OBLONG, so a
    // SMALL ROUND CREAM fruit with DARK-GREEN RIBS
    // on a vine sitting ON the slab still had no
    // sweet dumpling squash.
    public struct SweetDumplingPeg
    {
        public float x;
        public float face;
    }

    // Sit the kitchen kabocha ON the slab. Picture
    // only — not PlantFlower, not TreeRig.Sway,
    // not PlantStrawberry. Sweet dumpling owns the
    // SMALL ROUND CREAM fruit with DARK-GREEN RIBS
    // on a vine; carnival the ROUND CREAM globe with
    // ORANGE-GREEN MOTTLE; delicata the CREAM OBLONG
    // with DARK-GREEN STRIPES, so a SQUAT DARK-GREEN
    // globe with PALE SPECKLES on a vine sitting ON
    // the slab still had no kabocha squash.
    public struct KabochaPeg
    {
        public float x;
        public float face;
    }

    private struct Decor
    {
        public string name;
        public float x;
        public float centerY;
        public Vector2 size;
        public Color color;
        public string kind;
    }

    // Continuous tape −32…+96. Spawn at x=0, surface −2.00.
    public static readonly Slab[] Slabs =
    {
        new Slab { left = -32f, width = 32f, surfaceY = -2.00f, name = "Behind" },
        new Slab { left =   0f, width =  8f, surfaceY = -2.00f, name = "Spawn" },
        new Slab { left =   8f, width =  3f, surfaceY = -1.95f, name = "CurbEntry" },
        new Slab { left =  11f, width =  7f, surfaceY = -1.95f, name = "GardenWalk" },
        new Slab { left =  18f, width =  4f, surfaceY = -2.00f, name = "DrainDip" },
        new Slab { left =  22f, width =  6f, surfaceY = -1.95f, name = "CurbReturn" },
        new Slab { left =  28f, width = 12f, surfaceY = -1.90f, name = "CourtyardSquare" },
        new Slab { left =  40f, width =  2f, surfaceY = -1.95f, name = "CurbGateDip" },
        new Slab { left =  42f, width =  8f, surfaceY = -1.90f, name = "FinishPlaza" },
        new Slab { left =  50f, width =  6f, surfaceY = -1.85f, name = "EastRise" },
        new Slab { left =  56f, width =  4f, surfaceY = -1.90f, name = "RainGutter" },
        new Slab { left =  60f, width =  6f, surfaceY = -1.95f, name = "LowerWalk" },
        new Slab { left =  66f, width = 10f, surfaceY = -1.90f, name = "OrchardWalk" },
        new Slab { left =  76f, width =  4f, surfaceY = -1.85f, name = "CurbUpper" },
        new Slab { left =  80f, width =  8f, surfaceY = -1.90f, name = "UpperCourt" },
        new Slab { left =  88f, width =  4f, surfaceY = -1.85f, name = "CurbLookout" },
        new Slab { left =  92f, width =  2f, surfaceY = -1.80f, name = "Lookout" },
        new Slab { left =  94f, width =  2f, surfaceY = -1.85f, name = "AfterWalk" }
    };

    // Matches BackFence*RailHigh* (center −1.55, height 0.07).
    public static readonly FenceRail[] FenceRails =
    {
        new FenceRail { x = -27.50f, width = 5.90f, topY = -1.515f },
        new FenceRail { x = -18.325f, width = 2.25f, topY = -1.515f },
        new FenceRail { x = -15.675f, width = 2.25f, topY = -1.515f },
        new FenceRail { x = -6.00f, width = 5.90f, topY = -1.515f }
    };

    // East posts (−9 / −3). Top of FencePostSize 0.62 at center −1.69 is −1.38.
    // Hang stays short so the cloth clears the high rail (−1.515) and the
    // sparrows that hop it. World spawn (x=0) and Level1 (−8) both see this bay.
    public static readonly Clothesline[] Clotheslines =
    {
        new Clothesline { x0 = -9f, x1 = -3f, y = -1.38f }
    };

    // East low rail (opening World x=0 and Level1 x=−8) and the courtyard
    // bench (World fire at 32.5). Low-rail top −1.785; loaf sits on it.
    public static readonly CatPerch[] CatPerches =
    {
        new CatPerch { x = -7.4f, y = -1.75f, face = 1f },
        new CatPerch { x = 34.15f, y = -1.305f, face = -1f }
    };

    // East post hangs into the World/Level1 opening. SpawnLampPost fills the
    // empty +X half of World at x=0. Entry and gate posts were empty sticks.
    public static readonly LanternPeg[] LanternPegs =
    {
        new LanternPeg { x = -3f, y = -1.38f, hang = 0.08f },
        new LanternPeg { x = 3.25f, y = -0.92f, hang = 0.06f },
        new LanternPeg { x = 8.35f, y = -1.63f, hang = 0.05f },
        new LanternPeg { x = 10.65f, y = -1.63f, hang = -0.05f },
        new LanternPeg { x = 40.25f, y = -1.31f, hang = 0.06f },
        new LanternPeg { x = 41.75f, y = -1.31f, hang = -0.06f }
    };

    // Inner face of GatePostWest. Leaf stays short of the east post so the
    // 1.5 m bay still reads as a walk-through. restZ tips the free end up
    // off the grass. Picture, no collider.
    public static readonly GateHinge[] GateHinges =
    {
        new GateHinge { x = 40.29f, y = -1.63f, width = 0.88f, height = 0.50f, restZ = 5f }
    };

    // East high rail: between laundry pegs so the hang stays clear.
    // −8.50 is the Level1 opening (player −8); −4.62 is the left edge of
    // World at x=0. West rail matches the far fence. Rail top −1.515.
    public static readonly NestPeg[] NestPegs =
    {
        new NestPeg { x = -8.50f, y = -1.515f, face = 1f },
        new NestPeg { x = -4.62f, y = -1.515f, face = 1f },
        new NestPeg { x = -29.85f, y = -1.515f, face = -1f }
    };

    // World opening +X was grass around the lamp. GardenWalk is a named
    // slab with no garden (PlantFlower stays on scene Tree). −13.20 is the
    // Level1 dirt west of the east fence, clear of hedgehog / fire / berries.
    // Bees hover the bloom; snails climb the clay (YardAmbient). Same Xs,
    // not a second list.
    public static readonly FlowerPotPeg[] FlowerPotPegs =
    {
        new FlowerPotPeg { x = -13.20f, face = 1f },
        new FlowerPotPeg { x = 3.55f, face = 1f },
        new FlowerPotPeg { x = 9.50f, face = -1f },
        new FlowerPotPeg { x = 12.20f, face = 1f },
        new FlowerPotPeg { x = 16.80f, face = -1f }
    };

    // East post L sits in the Level1 opening (player −8). y is between the
    // two rails so the clothesline at −1.38 stays clear. World +X past the
    // lamp pot was still grass; CurbReturn is a named empty slab on the
    // walk to the fire. Stake houses draw their own pole down to the slab.
    public static readonly BirdhousePeg[] BirdhousePegs =
    {
        new BirdhousePeg { x = -9.00f, y = -1.685f, face = 1f, stake = false },
        new BirdhousePeg { x = 5.20f, y = 0f, face = -1f, stake = true },
        new BirdhousePeg { x = 25.15f, y = 0f, face = 1f, stake = true }
    };

    // Houses and nests fill the night; the slab still had nowhere to drink.
    // −11.40 is Level1 west dirt between the pot and the east fence (clear of
    // hedgehog / berries / fire). 6.65 is World +X past the stake house,
    // before the curb. GardenWalk 14.50 sits between pots. 36.40 is the
    // courtyard east of the bench (World fire shot).
    public static readonly BirdBathPeg[] BirdBathPegs =
    {
        new BirdBathPeg { x = -11.40f, face = 1f },
        new BirdBathPeg { x = 6.65f, face = -1f },
        new BirdBathPeg { x = 14.50f, face = 1f },
        new BirdBathPeg { x = 36.40f, face = -1f }
    };

    // Baths and houses filled the drink / roost; the slab still had no
    // amphibian. −12.30 is Level1 west between the pot and the bath.
    // 4.40 is World +X between the lamp pot and the stake house.
    // 19.15 sits on DrainDip puddle u1; 59.10 on RainGutter puddle u2.
    public static readonly FrogPeg[] FrogPegs =
    {
        new FrogPeg { x = -12.30f, face = 1f },
        new FrogPeg { x = 4.40f, face = -1f },
        new FrogPeg { x = 19.15f, face = 1f },
        new FrogPeg { x = 59.10f, face = -1f }
    };

    // Frogs sit the rim; the water column was still empty air. −11.40 and
    // 6.65 hover the bath bowls (opening Level1 / World). 20.00 / 58.00 sit
    // the DrainDip and RainGutter grates, clear of the rim toads.
    public static readonly DragonflyPeg[] DragonflyPegs =
    {
        new DragonflyPeg { x = -11.40f, face = 1f },
        new DragonflyPeg { x = 6.65f, face = -1f },
        new DragonflyPeg { x = 20.00f, face = 1f },
        new DragonflyPeg { x = 58.00f, face = -1f }
    };

    // Snails own the pot clay; post lumber was still bare. −9 is the
    // Level1 east post under the birdhouse. −3 is the lantern post in
    // both openings. 3.25 is the World lamp below the hang. 40.25 is
    // the west gate post facing the courtyard. y is body rest on the
    // wood, below lanterns / houses. YardAmbient does the push-up.
    public static readonly LizardPeg[] LizardPegs =
    {
        new LizardPeg { x = -9.00f, y = -1.86f, face = 1f },
        new LizardPeg { x = -3.00f, y = -1.86f, face = 1f },
        new LizardPeg { x = 3.25f, y = -1.48f, face = -1f },
        new LizardPeg { x = 40.25f, y = -1.70f, face = -1f }
    };

    // Lizards own the lumber; the dry grass between furniture was still
    // empty (frogs sit wet rims). −10.20 is Level1 west dirt between the
    // bath and the east fence. 1.80 is World +X before the lamp. 13.50
    // sits GardenWalk between a pot and the bath. 23.40 is CurbReturn
    // between the grate and the stake house. YardAmbient does the hop.
    public static readonly GrasshopperPeg[] GrasshopperPegs =
    {
        new GrasshopperPeg { x = -10.20f, face = 1f },
        new GrasshopperPeg { x = 1.80f, face = -1f },
        new GrasshopperPeg { x = 13.50f, face = 1f },
        new GrasshopperPeg { x = 23.40f, face = -1f }
    };

    // Hoppers own the blades; the bare dirt between furniture was still
    // empty (frogs sit wet rims). −7.15 is Level1 opening dirt between
    // the player (−8) and the hedgehog. −0.55 is the berry approach
    // toward the apple. 2.45 is World +X between the hopper and the
    // lamp. 31.20 is CourtyardSquare west of the World fire.
    // YardAmbient does the crawl.
    public static readonly AntPeg[] AntPegs =
    {
        new AntPeg { x = -7.15f, face = 1f },
        new AntPeg { x = -0.55f, face = -1f },
        new AntPeg { x = 2.45f, face = 1f },
        new AntPeg { x = 31.20f, face = 1f }
    };

    // Hoppers own the day blades; night grass was still empty (hoppers
    // hide, ants sink). −8.55 is Level1 opening dirt at the player's
    // feet (east of the birdhouse post). 0.55 is World spawn +X before
    // the hopper. 15.15 sits GardenWalk east of the bath. 33.05 is
    // CourtyardSquare between the World fire and the bench cat.
    // YardAmbient does the chirp.
    public static readonly CricketPeg[] CricketPegs =
    {
        new CricketPeg { x = -8.55f, face = 1f },
        new CricketPeg { x = 0.55f, face = -1f },
        new CricketPeg { x = 15.15f, face = 1f },
        new CricketPeg { x = 33.05f, face = -1f }
    };

    // Crickets own the night blades; ants sink, so the dirt was empty.
    // −5.45 is Level1 between the hedgehog and the drawn fire. −3.70 is
    // east of that fire / west of the berries — both openings see it.
    // 7.40 is World +X between the bath and the entry curb. 31.70 is
    // CourtyardSquare west of the World fire (ants 31.20 sink at dusk).
    // YardAmbient does the crawl.
    public static readonly WormPeg[] WormPegs =
    {
        new WormPeg { x = -5.45f, face = 1f },
        new WormPeg { x = -3.70f, face = -1f },
        new WormPeg { x = 7.40f, face = 1f },
        new WormPeg { x = 31.70f, face = 1f }
    };

    // Worms own the night dirt; frogs sit the rim and dragonflies the
    // grate air, so the puddle film was empty. 18.50 / 21.45 are DrainDip
    // puddles u0 / u2 (grate 20, frog 19.15 on u1). 56.50 / 59.55 are
    // RainGutter u0 / u2 (grate 58, frog 59.10 on the west of u2).
    // YardAmbient does the skate.
    public static readonly StriderPeg[] StriderPegs =
    {
        new StriderPeg { x = 18.50f, face = 1f },
        new StriderPeg { x = 21.45f, face = -1f },
        new StriderPeg { x = 56.50f, face = 1f },
        new StriderPeg { x = 59.55f, face = -1f }
    };

    // Striders own the puddle film; moths own the campfire; sky flocks
    // roost, so lantern air was empty. −3 is the east-fence hang both
    // openings see. 3.25 is the World spawn lamp. 10.65 is the east
    // entry post (8.35 stays a lantern without a second bat in chunk 0).
    // 40.25 is the west gate, facing the courtyard fire. y / hang match
    // LanternPegs so the orbit sits on the bowl. YardAmbient does the hunt.
    public static readonly BatPeg[] BatPegs =
    {
        new BatPeg { x = -3f, y = -1.38f, hang = 0.08f, face = 1f },
        new BatPeg { x = 3.25f, y = -0.92f, hang = 0.06f, face = -1f },
        new BatPeg { x = 10.65f, y = -1.63f, hang = -0.05f, face = 1f },
        new BatPeg { x = 40.25f, y = -1.31f, hang = 0.06f, face = -1f }
    };

    // Sparrows hop the high-rail top, laundry hangs the post line, cats
    // loaf the low rail, so the bay BETWEEN the rails was empty. −5.20 is
    // the east bay both openings see (nest −4.62 sits the cap, cat −7.4
    // loafs the low rail). −3.90 is the World-left edge of that bay, short
    // of the lantern post. −27.50 is the west bay. −17.00 fills the
    // authored break in the middle high rail (chunk −2). y is the gap
    // midpoint (high bottom −1.585, low top −1.785). YardAmbient does
    // the crawl.
    public static readonly WebPeg[] WebPegs =
    {
        new WebPeg { x = -27.50f, y = -1.685f, width = 0.24f, height = 0.20f, face = -1f },
        new WebPeg { x = -17.00f, y = -1.685f, width = 0.34f, height = 0.22f, face = 1f },
        new WebPeg { x = -5.20f, y = -1.685f, width = 0.24f, height = 0.20f, face = 1f },
        new WebPeg { x = -3.90f, y = -1.685f, width = 0.22f, height = 0.20f, face = 1f }
    };

    // Webs own the rail bay; worms own a thin night sheen on dirt, so the
    // slab under the loafing cat and at the fire crumbs was empty. −7.85
    // sits west of the east-fence cat (−7.4) both openings see (cricket
    // −8.55, ant −7.15). −4.15 is Level1 fire crumbs (DrawnCampfireX
    // −4.6; worm −3.70). 2.90 is World lamp crumbs (ant 2.45, lamp 3.25).
    // 32.15 is World fire west (YardX 32.5; worm 31.70). YardAmbient does
    // the dart.
    public static readonly MousePeg[] MousePegs =
    {
        new MousePeg { x = -7.85f, face = 1f },
        new MousePeg { x = -4.15f, face = -1f },
        new MousePeg { x = 2.90f, face = 1f },
        new MousePeg { x = 32.15f, face = 1f }
    };

    // Mice own the night slab; hoppers own the insect hop in the blades,
    // so day grass at mammal scale was empty. −9.40 is Level1 west of the
    // player (−8), east of the hopper (−10.20). 1.25 is World spawn +X
    // between the cricket (0.55) and the hopper (1.80). 11.70 sits
    // GardenWalk west of the pot (12.20). 35.20 is CourtyardSquare east
    // of the bench cat (34). YardAmbient does the nibble.
    public static readonly RabbitPeg[] RabbitPegs =
    {
        new RabbitPeg { x = -9.40f, face = 1f },
        new RabbitPeg { x = 1.25f, face = -1f },
        new RabbitPeg { x = 11.70f, face = 1f },
        new RabbitPeg { x = 35.20f, face = -1f }
    };

    // Rabbits own the grass; lizards own the post lumber; house sitters
    // take the hole after dusk, so the roofs were empty by day. −9.00 is
    // the Level1 east-post house (lizard −1.86 under it). 5.20 is the
    // World stake house. 10.65 is the east entry post under the lantern
    // (no lizard; bats hunt that air at night). 25.15 is CurbReturn.
    // y is body rest on the roof / cap. YardAmbient does the scamper.
    public static readonly SquirrelPeg[] SquirrelPegs =
    {
        new SquirrelPeg { x = -9.00f, y = -1.589f, face = 1f },
        new SquirrelPeg { x = 5.20f, y = -1.384f, face = -1f },
        new SquirrelPeg { x = 10.65f, y = -1.72f, face = -1f },
        new SquirrelPeg { x = 25.15f, y = -1.333f, face = 1f }
    };

    // Squirrels own the house roofs by day; bats hunt the hanging bowl,
    // so the lantern post cap was empty. −3 is the Level1 east-fence
    // lantern (opening shot; lizard lower on the lumber). 3.25 is the
    // World path lamp. 8.35 is the west entry post (bats skip that air).
    // 40.25 is the west gate facing the courtyard. y is body rest on the
    // cap, above the hang. 10.65 stays squirrel + bat. YardAmbient does
    // the roost.
    public static readonly OwlPeg[] OwlPegs =
    {
        new OwlPeg { x = -3.00f, y = -1.342f, face = 1f },
        new OwlPeg { x = 3.25f, y = -0.882f, face = -1f },
        new OwlPeg { x = 8.35f, y = -1.592f, face = 1f },
        new OwlPeg { x = 40.25f, y = -1.272f, face = -1f }
    };

    // Owls own the post cap; mice dart the crumb line beside the fire,
    // moths the flame air, so the log FLANK was empty. −5.30 is Level1
    // west of DrawnCampfireX (−4.6; hedgehog −6.4, mouse −4.15 east).
    // −0.90 is the berry approach both openings see (strawberries −1.2,
    // ant −0.55). 6.20 is World +X west of the bath (6.65; house 5.20)
    // after the drinker fades. 32.88 is World fire east of YardX 32.5
    // (mouse 32.15 west, cricket 33.05, bench leg 33.35). YardAmbient
    // does the wash.
    public static readonly RaccoonPeg[] RaccoonPegs =
    {
        new RaccoonPeg { x = -5.30f, face = 1f },
        new RaccoonPeg { x = -0.90f, face = 1f },
        new RaccoonPeg { x = 6.20f, face = 1f },
        new RaccoonPeg { x = 32.88f, face = -1f }
    };

    // Raccoons wash the log flank after dusk; frogs hop the dry rim and
    // striders skate the film, so the puddle BANK was empty. −10.70 is
    // Level1 west dirt east of the bath (−11.40; hopper −10.20) — opening
    // shot. 17.55 is GardenWalk east looking into DrainDip (18; pot
    // 16.80). 22.60 is CurbReturn west looking back at the dip (strider
    // 21.45, hopper 23.40). 55.70 is EastRise east looking into
    // RainGutter (56; strider 56.50). YardAmbient does the bask.
    public static readonly TurtlePeg[] TurtlePegs =
    {
        new TurtlePeg { x = -10.70f, face = -1f },
        new TurtlePeg { x = 17.55f, face = 1f },
        new TurtlePeg { x = 22.60f, face = -1f },
        new TurtlePeg { x = 55.70f, face = 1f }
    };

    // Turtles own the mud bank; striders own the film; drinkers own the
    // bowl rim, so the water's EDGE still had no waterfowl. −14.15 is
    // Level1 west dirt west of the pot (−13.20) — opening shot, clear of
    // frog (−12.30) / bath (−11.40) / turtle (−10.70). 7.20 is World +X
    // east of the bath (6.65; raccoon 6.20 west) before CurbEntry (8).
    // 20.55 is DrainDip east of the grate (20; frog 19.15, strider 21.45).
    // 57.40 is RainGutter between strider 56.50 and grate 58. YardAmbient
    // does the dabble.
    public static readonly DuckPeg[] DuckPegs =
    {
        new DuckPeg { x = -14.15f, face = 1f },
        new DuckPeg { x = 7.20f, face = -1f },
        new DuckPeg { x = 20.55f, face = -1f },
        new DuckPeg { x = 57.40f, face = 1f }
    };

    // Ducks loaf the edge; striders skate the film; frogs hop the rim,
    // so nobody WADES the dip. 18.28 is DrainDip west of the grate
    // (strider 18.50, turtle 17.55 on the bank). 21.72 is east of the
    // duck (20.55) before CurbReturn. 56.88 is RainGutter west of the
    // duck (57.40). 58.72 is east of the grate (58; frog 59.10).
    // YardAmbient does the spear.
    public static readonly HeronPeg[] HeronPegs =
    {
        new HeronPeg { x = 18.28f, face = 1f },
        new HeronPeg { x = 21.72f, face = -1f },
        new HeronPeg { x = 56.88f, face = 1f },
        new HeronPeg { x = 58.72f, face = -1f }
    };

    // Herons wade; striders skate the film; ducks loaf the edge, so the
    // OPEN water under the ellipse still had no prey. 18.85 is DrainDip
    // between strider 18.50 and frog 19.15 (heron 18.28 spears from the
    // west). 21.10 is east of the duck (20.55) before strider 21.45.
    // 57.12 is RainGutter u1 between heron 56.88 and duck 57.40.
    // 58.95 is east of heron 58.72 before frog 59.10. YardAmbient darts.
    public static readonly MinnowPeg[] MinnowPegs =
    {
        new MinnowPeg { x = 18.85f, face = 1f },
        new MinnowPeg { x = 21.10f, face = -1f },
        new MinnowPeg { x = 57.12f, face = 1f },
        new MinnowPeg { x = 58.95f, face = -1f }
    };

    // Minnows dart the film; ducks loaf the edge; turtles own the mud,
    // so the wet MARGIN still had no reed (grass skips IsWetAt). −13.68
    // is Level1 between the duck (−14.15) and the pot (−13.20) — opening
    // shot. 7.76 is World +X east of the duck (7.20) before CurbEntry
    // (worm 7.40 west). 17.90 is DrainDip west bank (turtle 17.55,
    // heron 18.28 in the dip). 55.32 is RainGutter west (turtle 55.70).
    // YardAmbient does the sway.
    public static readonly CattailPeg[] CattailPegs =
    {
        new CattailPeg { x = -13.68f, face = 1f },
        new CattailPeg { x = 7.76f, face = -1f },
        new CattailPeg { x = 17.90f, face = 1f },
        new CattailPeg { x = 55.32f, face = 1f }
    };

    // Cattails own the bank; minnows dart under the film; striders
    // skate the ellipse, so the OPEN water still had no plant. 19.36
    // is DrainDip puddle u1 east of the frog (19.15), short of the
    // grate (20). 21.22 is u2 west of the strider (21.45; minnow
    // 21.10 under). 56.28 is RainGutter u0 west of the strider
    // (56.50). 59.22 is u2 between the frog (59.10) and the strider
    // (59.55). YardAmbient does the sway.
    public static readonly LilyPadPeg[] LilyPadPegs =
    {
        new LilyPadPeg { x = 19.36f, face = 1f },
        new LilyPadPeg { x = 21.22f, face = -1f },
        new LilyPadPeg { x = 56.28f, face = 1f },
        new LilyPadPeg { x = 59.22f, face = -1f }
    };

    // Cattails own the wet bank; lily pads the film; terracotta
    // owns the pots, so DRY grass was still only tufts. −9.82 is
    // Level1 west between the hopper (−10.20) and the rabbit
    // (−9.40) — opening shot, clear of the east-fence rail NearDecor.
    // 4.80 is World +X between the frog (4.40) and the stake house
    // (5.20). 14.00 sits GardenWalk between the hopper (13.50) and
    // the bath (14.50). 24.28 is CurbReturn between the hopper
    // (23.40) and the stake house (25.15). YardAmbient does the sway.
    public static readonly DandelionPeg[] DandelionPegs =
    {
        new DandelionPeg { x = -9.82f, face = 1f },
        new DandelionPeg { x = 4.80f, face = -1f },
        new DandelionPeg { x = 14.00f, face = 1f },
        new DandelionPeg { x = 24.28f, face = -1f }
    };

    // Dandelions own the dry blades; cattails the wet bank; lily
    // pads the film, so the DAMP MARGIN of a bowl / dip still had
    // no toadstool. −11.85 is Level1 between the frog (−12.30) and
    // the bath (−11.40) — opening shot. 5.70 is World +X between
    // the stake house (5.20) and the raccoon (6.20) before the
    // bath (6.65). 23.00 sits CurbReturn east of DrainDip (turtle
    // 22.60, hopper 23.40). 60.80 is LowerWalk east of RainGutter
    // (curb 60, empty stretch before orchard 68). YardAmbient
    // does the sway.
    public static readonly MushroomPeg[] MushroomPegs =
    {
        new MushroomPeg { x = -11.85f, face = 1f },
        new MushroomPeg { x = 5.70f, face = -1f },
        new MushroomPeg { x = 23.00f, face = 1f },
        new MushroomPeg { x = 60.80f, face = 1f }
    };

    // Mushrooms own the damp margin; lizards the lumber face;
    // lanterns hang; houses sit, so the POST still had no vine.
    // −9 is Level1 east post L (opening; birdhouse between the
    // rails, ivy stays the foot — lizard order 6 draws on top).
    // −14.50 is the middle-fence east post (no lantern / house).
    // 3.25 is the World lamp (opening; pot 3.55 east, so face −1).
    // 41.75 is the east gate post (lantern, no lizard; west 40.25
    // keeps the ajar leaf). YardAmbient does the sway.
    public static readonly IvyPeg[] IvyPegs =
    {
        new IvyPeg { x = -14.50f, face = 1f },
        new IvyPeg { x = -9.00f, face = -1f },
        new IvyPeg { x = 3.25f, face = -1f },
        new IvyPeg { x = 41.75f, face = -1f }
    };

    // Ivy owns the post climb; mushrooms the damp margin; dandelions
    // the dry blades, so SHADE at a fence foot / grove still had no
    // frond. −15.60 is Level1 middle-fence bay (opening west; ivy
    // −14.50, web −17 in the previous chunk). 9.95 is World entry
    // bay between the pot (9.50) and the east post (10.65). 25.85
    // sits CurbReturn east of the stake house (25.15). 38.55 is
    // grove/gate shade (tree 37.5, west gate 40.25). YardAmbient
    // does the sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly FernPeg[] FernPegs =
    {
        new FernPeg { x = -15.60f, face = 1f },
        new FernPeg { x = 9.95f, face = -1f },
        new FernPeg { x = 25.85f, face = 1f },
        new FernPeg { x = 38.55f, face = -1f }
    };

    // Dandelions own the tall clocks; ferns the shade understory;
    // mushrooms the damp margin, so DRY lawn between tufts still had
    // no trefoil. −12.75 is Level1 between the pot (−13.20) and the
    // frog (−12.30) — opening west dirt. 2.35 is World +X between
    // the hopper (1.80) and the mouse (2.90). 12.85 sits GardenWalk
    // between the pot (12.20) and the hopper (13.50). 35.80 is
    // CourtyardSquare between the rabbit (35.20) and the bath
    // (36.40). YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly CloverPeg[] CloverPegs =
    {
        new CloverPeg { x = -12.75f, face = 1f },
        new CloverPeg { x = 2.35f, face = -1f },
        new CloverPeg { x = 12.85f, face = 1f },
        new CloverPeg { x = 35.80f, face = -1f }
    };

    // Clover owns the low dry carpet; dandelions the tall clocks;
    // ferns the shade foot, so DRY WASTE between furniture still
    // had no spine. −5.92 is Level1 between the hedgehog (−6.4)
    // and the worm (−5.45) — opening dirt under the east-fence
    // bay. 3.90 is World +X between the lamp pot (3.55) and the
    // frog (4.40). 26.40 sits CurbReturn east of the fern (25.85).
    // 39.20 is CourtyardSquare west of the gate (40.25), east of
    // the fern (38.55). YardAmbient does the sway. Chunk seams
    // at 16 m; stay 0.10 inside.
    public static readonly ThistlePeg[] ThistlePegs =
    {
        new ThistlePeg { x = -5.92f, face = 1f },
        new ThistlePeg { x = 3.90f, face = -1f },
        new ThistlePeg { x = 26.40f, face = 1f },
        new ThistlePeg { x = 39.20f, face = -1f }
    };

    // Thistles own the dry waste; clover the lawn carpet; ants the
    // dirt crawl, so the TRODDEN path edge still had no spike.
    // −6.78 is Level1 between the ants (−7.15) and the hedgehog
    // (−6.4) — walked dirt toward the apple. 0.90 is World spawn
    // +X between the cricket (0.55) and the rabbit (1.25). 15.85
    // sits GardenWalk between the cricket (15.15) and the pot
    // (16.80). 29.55 is CourtyardSquare west, between the curb
    // (28) and the ants (31.20). YardAmbient does the sway.
    // Chunk seams at 16 m; stay 0.10 inside.
    public static readonly PlantainPeg[] PlantainPegs =
    {
        new PlantainPeg { x = -6.78f, face = 1f },
        new PlantainPeg { x = 0.90f, face = -1f },
        new PlantainPeg { x = 15.85f, face = 1f },
        new PlantainPeg { x = 29.55f, face = -1f }
    };

    // Plantain owns the trodden edge; thistles the dry waste; ferns
    // the shade foot; ivy the post, so DAMP nitrogen at a fence or
    // grove drip still had no sting. −15.05 is Level1 middle-fence
    // bay between the fern (−15.60) and the ivy post (−14.50) —
    // opening west dirt. 8.90 is World CurbEntry between the west
    // lantern (8.35) and the pot (9.50). 17.20 sits GardenWalk
    // approaching DrainDip, between the pot (16.80) and the turtle
    // (17.55). 38.10 is CourtyardSquare grove drip, east of the
    // tree (37.5) and west of the fern (38.55). YardAmbient does
    // the sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly NettlePeg[] NettlePegs =
    {
        new NettlePeg { x = -15.05f, face = 1f },
        new NettlePeg { x = 8.90f, face = -1f },
        new NettlePeg { x = 17.20f, face = 1f },
        new NettlePeg { x = 38.10f, face = -1f }
    };

    // Nettles own the fence-foot sting; plantain the trodden
    // spike; thistles the dry waste; cattails the wet reed, so
    // OPEN disturbed nitrogen still had no rusty panicle.
    // −3.35 is Level1 between the worm (−3.70, east of the
    // drawn fire) and the lantern post (−3) — opening dirt
    // toward the berries. 7.00 is World +X between the bath
    // (6.65) and the duck (7.20), west of the cattail (7.76).
    // 16.40 sits GardenWalk between the plantain (15.85) and
    // the pot (16.80), west of the nettle (17.20). 30.38 is
    // CourtyardSquare between the plantain (29.55) and the
    // ants (31.20). YardAmbient does the sway. Chunk seams
    // at 16 m; stay 0.10 inside.
    public static readonly DockPeg[] DockPegs =
    {
        new DockPeg { x = -3.35f, face = 1f },
        new DockPeg { x = 7.00f, face = -1f },
        new DockPeg { x = 16.40f, face = 1f },
        new DockPeg { x = 30.38f, face = -1f }
    };

    // Docks own the open panicle; ivy the post; nettles the
    // sting, so a FENCE BAY still had no hedge. −11.05 is
    // Level1 west of the east fence, between the bath (−11.40)
    // and the turtle (−10.70). 2.70 is World spawn +X between
    // the clover (2.35) and the lamp (3.25). 24.80 sits
    // CurbReturn between the dandelion (24.28) and the stake
    // house (25.15). 39.55 is CourtyardSquare between the
    // thistle (39.20) and the west gate (40.25). YardAmbient
    // does the sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly BramblePeg[] BramblePegs =
    {
        new BramblePeg { x = -11.05f, face = 1f },
        new BramblePeg { x = 2.70f, face = -1f },
        new BramblePeg { x = 24.80f, face = 1f },
        new BramblePeg { x = 39.55f, face = -1f }
    };

    // Brambles own the bay hedge; ivy the post; pots the clay
    // bloom; strawberries the low fruit, so a SUNNY garden column
    // still had no spike. −2.40 is Level1 between the first two
    // strawberry bushes (−2.8 / −2.0) — opening garden, east of
    // the lantern (−3). 1.58 is World spawn +X between the rabbit
    // (1.25) and the hopper (1.80). 26.85 sits CurbReturn east of
    // the thistle (26.40), west of the square. 35.48 is
    // CourtyardSquare east of the bench (34 / leg 34.65), between
    // the rabbit (35.20) and the clover (35.80). YardAmbient does
    // the sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly HollyhockPeg[] HollyhockPegs =
    {
        new HollyhockPeg { x = -2.40f, face = 1f },
        new HollyhockPeg { x = 1.58f, face = -1f },
        new HollyhockPeg { x = 26.85f, face = 1f },
        new HollyhockPeg { x = 35.48f, face = -1f }
    };

    // Hollyhocks own the tall column; strawberries the low fruit;
    // pots the clay bloom, so a ROUND garden shrub still had no
    // rose. −1.55 is Level1 between the last two strawberry bushes
    // (−2.0 / −1.2) — opening garden, east of the hollyhock
    // (−2.40). 0.22 is World spawn +X (west of the cricket 0.55)
    // and Level1 apple foot. 27.42 sits CurbReturn east of the
    // hollyhock (26.85), west of the square. 36.95 is
    // CourtyardSquare east of the bath (36.40), west of the grove
    // tree (37.5). YardAmbient does the sway. Chunk seams at
    // 16 m; stay 0.10 inside.
    public static readonly RosePeg[] RosePegs =
    {
        new RosePeg { x = -1.55f, face = 1f },
        new RosePeg { x = 0.22f, face = -1f },
        new RosePeg { x = 27.42f, face = 1f },
        new RosePeg { x = 36.95f, face = -1f }
    };

    // Hollyhocks own the tall column; roses the round shrub;
    // pots the clay bloom, so a LOW herb border still had no
    // lavender. −0.20 is Level1 apple west foot (east of the
    // raccoon −0.90 / ant −0.55) and World spawn −X of the rose
    // (0.22). 15.50 is GardenWalk between the cricket (15.15)
    // and plantain (15.85), east of the bath (14.50). 23.80 sits
    // CurbReturn between the hopper (23.40) and the dandelion
    // (24.28). 29.10 is CourtyardSquare west of the plantain
    // (29.55), east of the curb (28). YardAmbient does the sway.
    // Chunk seams at 16 m; stay 0.10 inside.
    public static readonly LavenderPeg[] LavenderPegs =
    {
        new LavenderPeg { x = -0.20f, face = 1f },
        new LavenderPeg { x = 15.50f, face = -1f },
        new LavenderPeg { x = 23.80f, face = 1f },
        new LavenderPeg { x = 29.10f, face = -1f }
    };

    // Hollyhocks own the tall column; roses the round shrub;
    // lavender the low mound; cattails the wet bank; ferns the
    // shade, so a SWORD-LEAF garden fan still had no iris. −0.72
    // is Level1 apple approach east of the last strawberry
    // (−1.2) / rose (−1.55), west of lavender (−0.20). 2.08 is
    // World spawn +X between the hopper (1.80) and the clover
    // (2.35). 12.52 is GardenWalk between the pot (12.20) and
    // clover (12.85). 29.82 is CourtyardSquare east of the
    // plantain (29.55) / lavender (29.10), west of the dock
    // (30.38). YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly IrisPeg[] IrisPegs =
    {
        new IrisPeg { x = -0.72f, face = 1f },
        new IrisPeg { x = 2.08f, face = -1f },
        new IrisPeg { x = 12.52f, face = 1f },
        new IrisPeg { x = 29.82f, face = -1f }
    };

    // Hollyhocks own the tall column; roses the round shrub;
    // lavender the low mound; irises the sword fan, so a CUP-
    // ON-A-STEM garden bloom still had no tulip. −2.60 is
    // Level1 between the first strawberry (−2.8) and the
    // hollyhock (−2.40), east of the lantern (−3). 3.70 is
    // World spawn +X between the lamp pot (3.55) and the
    // thistle (3.90). 11.20 is GardenWalk west of the rabbit
    // (11.70), east of the entry post (10.65). 28.55 is
    // CourtyardSquare east of the curb (28), west of the
    // lavender (29.10). YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly TulipPeg[] TulipPegs =
    {
        new TulipPeg { x = -2.60f, face = 1f },
        new TulipPeg { x = 3.70f, face = -1f },
        new TulipPeg { x = 11.20f, face = 1f },
        new TulipPeg { x = 28.55f, face = -1f }
    };

    // Hollyhocks own the tall column; roses the round shrub;
    // lavender the low mound; irises the sword fan; tulips the
    // cup, so a TRUMPET corona still had no daffodil. −1.78 is
    // Level1 between the middle strawberry (−2.0) and the rose
    // (−1.55). 1.08 is World spawn +X between the plantain
    // (0.90) and the rabbit (1.25). 13.18 is GardenWalk between
    // the clover (12.85) and the hopper (13.50). 28.82 is
    // CourtyardSquare east of the tulip (28.55), west of
    // lavender (29.10). YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly DaffodilPeg[] DaffodilPegs =
    {
        new DaffodilPeg { x = -1.78f, face = 1f },
        new DaffodilPeg { x = 1.08f, face = -1f },
        new DaffodilPeg { x = 13.18f, face = 1f },
        new DaffodilPeg { x = 28.82f, face = -1f }
    };

    // Hollyhocks own the tall column; roses the round shrub;
    // lavender the low mound; irises the sword fan; tulips the
    // cup; daffodils the trumpet; dandelions the wild clock, so
    // a RAY-DISK still had no daisy. −2.20 is Level1 between
    // the hollyhock (−2.40) and the middle strawberry (−2.0).
    // 2.22 is World spawn +X between the iris (2.08) and the
    // clover (2.35). 11.45 is GardenWalk between the tulip
    // (11.20) and the rabbit (11.70). 28.28 is CourtyardSquare
    // east of the curb (28), west of the tulip (28.55).
    // YardAmbient does the sway. Chunk seams at 16 m; stay
    // 0.10 inside.
    public static readonly DaisyPeg[] DaisyPegs =
    {
        new DaisyPeg { x = -2.20f, face = 1f },
        new DaisyPeg { x = 2.22f, face = -1f },
        new DaisyPeg { x = 11.45f, face = 1f },
        new DaisyPeg { x = 28.28f, face = -1f }
    };

    // Hollyhocks own the tall column; roses the round shrub;
    // lavender the low mound; irises the sword fan; tulips the
    // cup; daffodils the trumpet; daisies the ray-disk;
    // dandelions the wild clock, so a PAPER BOWL still had no
    // poppy. −1.38 is Level1 between the rose (−1.55) and the
    // last strawberry (−1.2). 1.40 is World spawn +X between
    // the rabbit (1.25) and the hollyhock (1.58). 11.95 is
    // GardenWalk between the rabbit (11.70) and the pot
    // (12.20). 30.10 is CourtyardSquare east of the iris
    // (29.82), west of the dock (30.38). YardAmbient does the
    // sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly PoppyPeg[] PoppyPegs =
    {
        new PoppyPeg { x = -1.38f, face = 1f },
        new PoppyPeg { x = 1.40f, face = -1f },
        new PoppyPeg { x = 11.95f, face = 1f },
        new PoppyPeg { x = 30.10f, face = -1f }
    };

    // Hollyhocks own the tall column; roses the round shrub;
    // lavender the low mound; irises the sword fan; tulips the
    // cup; daffodils the trumpet; daisies the ray-disk; poppies
    // the paper bowl; lily pads the puddle film, so a SIX-POINT
    // flare still had no lily. −0.38 is Level1 apple approach
    // between the ant (−0.55) and lavender (−0.20). 0.38 is
    // World spawn +X between the rose (0.22) and the cricket
    // (0.55). 13.75 is GardenWalk between the hopper (13.50)
    // and the dandelion (14.00). 30.78 is CourtyardSquare east
    // of the dock (30.38), west of the ants (31.20). YardAmbient
    // does the sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly LilyPeg[] LilyPegs =
    {
        new LilyPeg { x = -0.38f, face = 1f },
        new LilyPeg { x = 0.38f, face = -1f },
        new LilyPeg { x = 13.75f, face = 1f },
        new LilyPeg { x = 30.78f, face = -1f }
    };

    // Hollyhocks own the tall column; roses the round shrub;
    // lavender the low mound; irises the sword fan; tulips the
    // cup; daffodils the trumpet; daisies the small ray-disk;
    // poppies the paper bowl; lilies the six-point flare;
    // dandelions the wild clock, so a GIANT disk-on-a-stalk still
    // had no sunflower. −3.18 is Level1 between the dock (−3.35)
    // and the lantern (−3), west of the first strawberry (−2.8)
    // — opening garden, the packed berry row has no room for a
    // wide head. 4.15 is World spawn +X between the thistle
    // (3.90) and the frog (4.40), west of the dandelion (4.80).
    // 14.25 is GardenWalk between the dandelion (14.00) and the
    // bath (14.50). 36.10 is CourtyardSquare between the clover
    // (35.80) and the bath (36.40), west of the rose (36.95).
    // YardAmbient does the sway. Chunk seams at 16 m; stay 0.10
    // inside.
    public static readonly SunflowerPeg[] SunflowerPegs =
    {
        new SunflowerPeg { x = -3.18f, face = 1f },
        new SunflowerPeg { x = 4.15f, face = -1f },
        new SunflowerPeg { x = 14.25f, face = 1f },
        new SunflowerPeg { x = 36.10f, face = -1f }
    };

    // Hollyhocks own the tall column; roses the round shrub;
    // lavender the spike mound; irises the sword fan; sunflowers
    // the giant disk, so a LOW five-petal face still had no
    // violet. −10.45 is Level1 between the turtle (−10.70) and
    // the hopper (−10.20), east of the bath (−11.40) — opening
    // damp-to-dry edge, clear of the packed sunny bed. 5.45 is
    // World spawn +X between the stake house (5.20) and the
    // mushroom (5.70). 9.20 is CurbEntry between the nettle
    // (8.90) and the pot (9.50), west of the fern (9.95).
    // 25.50 is CurbReturn between the stake house (25.15) and
    // the fern (25.85). Shade feet, not the sunny column.
    // YardAmbient does the sway. Chunk seams at 16 m; stay
    // 0.10 inside.
    public static readonly VioletPeg[] VioletPegs =
    {
        new VioletPeg { x = -10.45f, face = 1f },
        new VioletPeg { x = 5.45f, face = -1f },
        new VioletPeg { x = 9.20f, face = 1f },
        new VioletPeg { x = 25.50f, face = -1f }
    };

    // Violets own the five-petal face; tulips the closed cup, so
    // a SHORT open goblet still had no crocus. −12.08 is Level1
    // between the frog (−12.30) and the mushroom (−11.85), east
    // of the clover (−12.75) — opening cool lawn, not the damp
    // cushion at −10.45. 5.95 is World spawn +X between the
    // mushroom (5.70) and the raccoon (6.20), west of the bath
    // (6.65), other side of the mushroom from the violet (5.45).
    // 14.82 is GardenWalk between the bath (14.50) and the
    // cricket (15.15). 26.12 is CurbReturn between the fern
    // (25.85) and the thistle (26.40). Cool turf, not the sunny
    // column. YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly CrocusPeg[] CrocusPegs =
    {
        new CrocusPeg { x = -12.08f, face = 1f },
        new CrocusPeg { x = 5.95f, face = -1f },
        new CrocusPeg { x = 14.82f, face = 1f },
        new CrocusPeg { x = 26.12f, face = -1f }
    };

    // Crocuses own the short goblet; violets the five-petal face;
    // lavender the upright spike, so a hanging bell cluster still
    // had no bluebell. −13.92 is Level1 between the duck (−14.15)
    // and the cattail (−13.68) — opening damp bank, not the cool
    // lawn goblet at −12.08. 6.42 is World spawn +X between the
    // raccoon (6.20) and the bath (6.65), other side of the bath
    // from the crocus (5.95). 8.70 is CurbEntry between the
    // lantern (8.35) and the nettle (8.90), damp fence-foot.
    // 38.32 is CourtyardSquare grove drip between the nettle
    // (38.10) and the fern (38.55). Woodland bells, not the sunny
    // column. YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly BluebellPeg[] BluebellPegs =
    {
        new BluebellPeg { x = -13.92f, face = 1f },
        new BluebellPeg { x = 6.42f, face = -1f },
        new BluebellPeg { x = 8.70f, face = 1f },
        new BluebellPeg { x = 38.32f, face = -1f }
    };

    // Bluebells own the hanging bell; hollyhocks the bloom column;
    // sunflowers the disk-on-a-stalk; strawberries the low edible
    // bush (−2.8 / −2.0 / −1.2), so a staked kitchen vine still
    // had no tomato. −3.52 is Level1 between the worm (−3.70) and
    // the dock (−3.35), west of the first strawberry — kitchen
    // edge of the berry plot, not the woodland raceme at −13.92.
    // 4.58 is World spawn +X between the frog (4.40) and the
    // dandelion (4.80), east of the sunflower (4.15). 15.32 is
    // GardenWalk between the cricket (15.15) and lavender (15.50).
    // 29.36 is CourtyardSquare between lavender (29.10) and the
    // plantain (29.55). Sunny kitchen bed, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m; stay 0.10
    // inside.
    public static readonly TomatoPeg[] TomatoPegs =
    {
        new TomatoPeg { x = -3.52f, face = 1f },
        new TomatoPeg { x = 4.58f, face = -1f },
        new TomatoPeg { x = 15.32f, face = 1f },
        new TomatoPeg { x = 29.36f, face = -1f }
    };

    // Tomatoes own the staked vine; sunflowers the disk-on-a-stalk;
    // hollyhocks the bloom column; mushrooms the toadstool cap, so
    // a round WRAPPER on dirt still had no cabbage. −4.95 is Level1
    // between the raccoon (−5.30) and the drawn fire (−4.6), west
    // of the tomato stake — kitchen dirt, not the log wash and not
    // the vine. 5.00 is World spawn +X between the dandelion
    // (4.80) and the stake house (5.20), east of the tomato
    // (4.58). 16.18 is GardenWalk between the plantain (15.85)
    // and the dock (16.40). 30.99 is CourtyardSquare between the
    // lily (30.78) and the ants (31.20). Round head on the slab,
    // not the grove drip. YardAmbient does the sway. Chunk seams
    // at 16 m; stay 0.10 inside.
    public static readonly CabbagePeg[] CabbagePegs =
    {
        new CabbagePeg { x = -4.95f, face = 1f },
        new CabbagePeg { x = 5.00f, face = -1f },
        new CabbagePeg { x = 16.18f, face = 1f },
        new CabbagePeg { x = 30.99f, face = -1f }
    };

    // Cabbages own the round wrapper; tomatoes the staked vine;
    // sunflowers the disk-on-a-stalk; mushrooms the toadstool
    // cap, so a squat RIBBED gourd on a dirt vine still had no
    // pumpkin. −4.28 is Level1 between the drawn fire (−4.6)
    // and the mouse crumbs (−4.15), east of the cabbage (−4.95)
    // and west of the tomato stake — kitchen dirt, not the
    // wrapper and not the vine. 5.58 is World spawn +X between
    // the violet (5.45) and the mushroom (5.70), east of the
    // cabbage (5.00) / stake house (5.20). 16.58 is GardenWalk
    // between the dock (16.40) and the pot (16.80), east of the
    // cabbage (16.18). 31.45 is CourtyardSquare between the
    // ants (31.20) and the worm (31.70), east of the cabbage
    // (30.99), west of the World fire (32.5). Ribbed gourd on
    // the slab, not the grove drip. YardAmbient does the sway.
    // Chunk seams at 16 m; stay 0.10 inside.
    public static readonly PumpkinPeg[] PumpkinPegs =
    {
        new PumpkinPeg { x = -4.28f, face = 1f },
        new PumpkinPeg { x = 5.58f, face = -1f },
        new PumpkinPeg { x = 16.58f, face = 1f },
        new PumpkinPeg { x = 31.45f, face = -1f }
    };

    // Pumpkins own the ribbed gourd; cabbages the round wrapper;
    // tomatoes the staked vine; sunflowers the disk-on-a-stalk;
    // cattails the brown sausage, so a TALL TASSEL stalk with
    // strap leaves and a husked ear still had no corn. −5.68 is
    // Level1 between the thistle (−5.92) and the worm (−5.45),
    // west of the cabbage — kitchen verge, not the wrapper and
    // not the gourd. 6.08 is World spawn +X between the crocus
    // (5.95) and the raccoon (6.20), east of the pumpkin (5.58).
    // 17.00 is GardenWalk between the pot (16.80) and the nettle
    // (17.20), east of the pumpkin (16.58). 30.58 is
    // CourtyardSquare between the dock (30.38) and the lily
    // (30.78), west of the cabbage (30.99). Tall tassel on the
    // slab, not the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly CornPeg[] CornPegs =
    {
        new CornPeg { x = -5.68f, face = 1f },
        new CornPeg { x = 6.08f, face = -1f },
        new CornPeg { x = 17.00f, face = 1f },
        new CornPeg { x = 30.58f, face = -1f }
    };

    // Corn owns the tall tassel; pumpkins the ribbed gourd;
    // cabbages the round wrapper; tomatoes the staked vine;
    // ferns the shade pinna, so a TAPERING root in dirt with
    // ferny greens still had no carrot. −6.16 is Level1
    // between the hedgehog (−6.4) and the thistle (−5.92),
    // west of the corn — kitchen verge, not the tassel and
    // not the wrapper. 5.82 is World spawn +X between the
    // mushroom (5.70) and the crocus (5.95), east of the
    // pumpkin (5.58), west of the corn (6.08). 15.68 is
    // GardenWalk between lavender (15.50) and the plantain
    // (15.85), east of the tomato (15.32). 31.82 is
    // CourtyardSquare between the worm (31.70) and the mouse
    // (32.15), east of the pumpkin (31.45), west of the
    // World fire (32.5). Taper in the slab, not the grove
    // drip. YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly CarrotPeg[] CarrotPegs =
    {
        new CarrotPeg { x = -6.16f, face = 1f },
        new CarrotPeg { x = 5.82f, face = -1f },
        new CarrotPeg { x = 15.68f, face = 1f },
        new CarrotPeg { x = 31.82f, face = -1f }
    };

    // Carrots own the tapering root; corn the tall tassel;
    // tomatoes the staked vine; cattails the brown sausage, so
    // a BLANCHED SPEAR shaft with an upright leaf fan still had
    // no leek. −6.96 is Level1 between the ants (−7.15) and the
    // plantain (−6.78), west of the carrot — kitchen verge, not
    // the cone and not the tassel. 6.84 is World spawn +X
    // between the bath (6.65) and the dock (7.00), east of the
    // corn (6.08). 15.00 is GardenWalk between the crocus
    // (14.82) and the cricket (15.15), west of the tomato
    // (15.32). 27.85 is CourtyardSquare between the rose
    // (27.42) and the daisy (28.28), west of the kitchen row
    // (tomato 29.36) — not the fire logs at 32.5. Spear on the
    // slab, not the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly LeekPeg[] LeekPegs =
    {
        new LeekPeg { x = -6.96f, face = 1f },
        new LeekPeg { x = 6.84f, face = -1f },
        new LeekPeg { x = 15.00f, face = 1f },
        new LeekPeg { x = 27.85f, face = -1f }
    };

    // Leeks own the blanched spear; tomatoes the staked vine
    // with round red fruit; corn the tall tassel; cattails the
    // brown sausage, so a POLE with twining leaves and long
    // hanging pods still had no bean. −7.75 is Level1 between
    // the mouse (−7.85) and the ants (−7.15), west of the leek
    // — kitchen verge under the east-fence rail, not the spear
    // and not the cat on the rail (−7.4). 7.58 is World spawn
    // +X between the worm (7.40) and the cattail (7.76), east
    // of the leek (6.84). 14.66 is GardenWalk between the bath
    // (14.50) and the crocus (14.82), west of the leek (15.00).
    // 28.06 is CourtyardSquare between the leek (27.85) and the
    // daisy (28.28), west of the kitchen row (tomato 29.36) —
    // not the fire logs at 32.5. Pole on the slab, not the
    // grove drip. YardAmbient does the sway. Chunk seams at
    // 16 m; stay 0.10 inside.
    public static readonly BeanPeg[] BeanPegs =
    {
        new BeanPeg { x = -7.75f, face = 1f },
        new BeanPeg { x = 7.58f, face = -1f },
        new BeanPeg { x = 14.66f, face = 1f },
        new BeanPeg { x = 28.06f, face = -1f }
    };

    // Beans own the pole with hanging pods; leeks the
    // blanched spear; carrots the tapering cone, so a
    // ROUND papery bulb in dirt with tubular greens still
    // had no onion. −8.22 is Level1 between the cricket
    // (−8.55) and the mouse (−7.85), at the player's feet
    // (−8), west of the bean pole — opening kitchen dirt,
    // not the pod and not the spear. 8.12 is World
    // CurbEntry west between the cattail (7.76) and the
    // entry lantern (8.35), east of the bean (7.58). 13.88
    // is GardenWalk between the lily (13.75) and the
    // dandelion (14.00), west of the sunflower (14.25) /
    // bath (14.50) / bean pole (14.66). 27.64 is
    // CurbReturn between the rose (27.42) and the leek
    // (27.85), west of the kitchen row — not the fire
    // logs at 32.5. Globe in the slab, not the grove
    // drip. YardAmbient does the sway. Chunk seams at
    // 16 m; stay 0.10 inside.
    public static readonly OnionPeg[] OnionPegs =
    {
        new OnionPeg { x = -8.22f, face = 1f },
        new OnionPeg { x = 8.12f, face = -1f },
        new OnionPeg { x = 13.88f, face = 1f },
        new OnionPeg { x = 27.64f, face = -1f }
    };

    // Onions own the round papery globe; leeks the
    // blanched spear; beans the pole with hanging pods, so
    // a CLUSTER of cloves under paper still had no garlic.
    // −8.90 is Level1 between the rabbit (−9.40) and the
    // cricket (−8.55), west of the onion globe — opening
    // kitchen dirt at the east-fence foot, not the round
    // bulb and not the spear. 8.52 is World CurbEntry east
    // of the onion (8.12), between the entry lantern hang
    // (8.35) and the bluebell (8.70), east of the bean.
    // 13.32 is GardenWalk between the daffodil (13.18)
    // and the hopper (13.50), west of the lily (13.75) /
    // onion (13.88). 26.62 is CurbReturn between the thistle
    // (26.40) and the hollyhock (26.85), west of the rose
    // (27.42) / onion (27.64), west of the kitchen row —
    // not the fire logs at 32.5. Cluster in the slab, not
    // the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly GarlicPeg[] GarlicPegs =
    {
        new GarlicPeg { x = -8.90f, face = 1f },
        new GarlicPeg { x = 8.52f, face = -1f },
        new GarlicPeg { x = 13.32f, face = 1f },
        new GarlicPeg { x = 26.62f, face = -1f }
    };

    // Garlic owns the clove cluster; onions the round papery
    // globe; carrots the tapering cone, so a SMALL ROUND RED
    // root in dirt with a white tip and lobed greens still
    // had no radish. −9.62 is Level1 between the dandelion
    // (−9.82) and the rabbit (−9.40), west of the garlic
    // cluster — opening kitchen dirt west of the east-fence
    // rabbit, not the clove bunch and not the gold globe.
    // 9.05 is World CurbEntry between the nettle (8.90) and
    // the violet (9.20), east of the garlic (8.52) / entry
    // lantern. 12.68 is GardenWalk between the iris (12.52)
    // and the clover (12.85), west of the garlic (13.32) /
    // daffodil (13.18). 25.68 is CurbReturn between the
    // violet (25.50) and the fern (25.85), west of the
    // garlic (26.62) / crocus (26.12), west of the kitchen
    // row — not the fire logs at 32.5. Root in the slab,
    // not the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly RadishPeg[] RadishPegs =
    {
        new RadishPeg { x = -9.62f, face = 1f },
        new RadishPeg { x = 9.05f, face = -1f },
        new RadishPeg { x = 12.68f, face = 1f },
        new RadishPeg { x = 25.68f, face = -1f }
    };

    // Radishes own the small round red root; onions the
    // papery globe; carrots the tapering cone, so an
    // IRREGULAR brown tuber in dirt with eyes and pinnate
    // leaflets still had no potato. −10.01 is Level1 between
    // the dandelion (−9.82) and the hopper (−10.20), west of
    // the radish — opening kitchen dirt west of the east-fence
    // rabbit, not the crimson globule and not the gold globe.
    // 9.72 is World CurbEntry between the pot (9.50) and the
    // fern (9.95), east of the radish (9.05) / violet (9.20).
    // 12.36 is GardenWalk between the pot (12.20) and the
    // iris (12.52), west of the radish (12.68) / clover
    // (12.85). 25.32 is CurbReturn between the stake house
    // (25.15) and the violet (25.50), west of the radish
    // (25.68) / fern (25.85), west of the kitchen row —
    // not the fire logs at 32.5. Tuber in the slab, not
    // the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly PotatoPeg[] PotatoPegs =
    {
        new PotatoPeg { x = -10.01f, face = 1f },
        new PotatoPeg { x = 9.72f, face = -1f },
        new PotatoPeg { x = 12.36f, face = 1f },
        new PotatoPeg { x = 25.32f, face = -1f }
    };

    // Potatoes own the irregular brown tuber; radishes
    // the small round red root; onions the papery globe, so
    // a ROUND PURPLE root in dirt with red-veined greens
    // still had no beet. −10.88 is Level1 between the turtle
    // (−10.70) and the bramble (−11.05), west of the potato
    // — opening kitchen dirt west of the hopper, not the
    // russet kidney and not the crimson globule. 10.28 is
    // World CurbEntry between the fern (9.95) and the
    // lantern (10.65), east of the potato (9.72). 11.32 is
    // GardenWalk between the tulip (11.20) and the daisy
    // (11.45), west of the potato (12.36) / radish (12.68).
    // 24.54 is CurbReturn between the dandelion (24.28) and
    // the bramble (24.80), west of the potato (25.32) / stake
    // house (25.15), west of the kitchen row — not the fire
    // logs at 32.5. Crown in the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly BeetPeg[] BeetPegs =
    {
        new BeetPeg { x = -10.88f, face = 1f },
        new BeetPeg { x = 10.28f, face = -1f },
        new BeetPeg { x = 11.32f, face = 1f },
        new BeetPeg { x = 24.54f, face = -1f }
    };

    // Beets own the round purple crown; carrots the
    // orange taper; potatoes the irregular brown tuber,
    // so a CREAMY WIDE cone in dirt with celery-like
    // pinnate greens still had no parsnip. −11.22 is
    // Level1 between the bramble (−11.05) and the bath
    // (−11.40), west of the beet — opening kitchen dirt
    // west of the turtle, not the burgundy globe and
    // not the russet kidney. 10.82 is World CurbEntry
    // between the lantern (10.65) and the GardenWalk
    // curb (11.00), east of the beet (10.28). 11.58 is
    // GardenWalk between the daisy (11.45) and the
    // rabbit (11.70), east of the beet (11.32) / tulip
    // (11.20). 24.04 is CurbReturn between lavender
    // (23.80) and the dandelion (24.28), west of the
    // beet (24.54) / bramble (24.80), west of the
    // kitchen row — not the fire logs at 32.5. Cone in
    // the slab, not the grove drip. YardAmbient does
    // the sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly ParsnipPeg[] ParsnipPegs =
    {
        new ParsnipPeg { x = -11.22f, face = 1f },
        new ParsnipPeg { x = 10.82f, face = -1f },
        new ParsnipPeg { x = 11.58f, face = 1f },
        new ParsnipPeg { x = 24.04f, face = -1f }
    };

    // Parsnips own the creamy wide cone; beets the
    // round purple crown; carrots the orange taper, so
    // a ROUND WHITE globe in dirt with a PURPLE shoulder
    // still had no turnip. −11.62 is Level1 between the
    // bath (−11.40) and the mushroom (−11.85), west of
    // the parsnip — opening kitchen dirt west of the
    // turtle, not the ivory cone and not the burgundy
    // globe. 11.04 is World CurbEntry between the
    // GardenWalk curb (11.00) and the tulip (11.20),
    // east of the parsnip (10.82). 11.82 is GardenWalk
    // between the rabbit (11.70) and the poppy (11.95),
    // east of the parsnip (11.58) / daisy (11.45).
    // 23.60 is CurbReturn between the hopper (23.40)
    // and lavender (23.80), west of the parsnip (24.04)
    // / beet (24.54) / fire logs 32.5. Globe in the
    // slab, not the grove drip. YardAmbient does the
    // sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly TurnipPeg[] TurnipPegs =
    {
        new TurnipPeg { x = -11.62f, face = 1f },
        new TurnipPeg { x = 11.04f, face = -1f },
        new TurnipPeg { x = 11.82f, face = 1f },
        new TurnipPeg { x = 23.60f, face = -1f }
    };

    // Turnips own the round white globe; parsnips the
    // creamy wide cone; beets the round purple crown, so
    // a LARGER YELLOW globe with a PURPLE cap still had
    // no rutabaga. −12.52 is Level1 between the frog
    // (−12.30) and the clover (−12.75), west of the
    // turnip / mushroom — opening kitchen dirt west of
    // the bath, not the ivory globe and not the burgundy
    // crown. 10.48 is World CurbEntry between the beet
    // (10.28) and the lantern (10.65), east of the potato,
    // west of the parsnip (10.82). 13.02 is GardenWalk
    // between the clover (12.85) and the daffodil
    // (13.18), east of the radish (12.68) / iris (12.52).
    // 23.20 is CurbReturn between the mushroom (23.00)
    // and the hopper (23.40), west of the turnip (23.60)
    // / lavender (23.80) / fire logs 32.5. Globe in the
    // slab, not the grove drip. YardAmbient does the
    // sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly RutabagaPeg[] RutabagaPegs =
    {
        new RutabagaPeg { x = -12.52f, face = 1f },
        new RutabagaPeg { x = 10.48f, face = -1f },
        new RutabagaPeg { x = 13.02f, face = 1f },
        new RutabagaPeg { x = 23.20f, face = -1f }
    };

    // Rutabagas own the larger yellow globe in dirt; turnips
    // the round white globe; beets the round purple crown, so
    // a PALE-GREEN swollen STEM ON the slab with long crown
    // petioles still had no kohlrabi. −12.96 is Level1
    // between the clover (−12.75) and the pot (−13.20),
    // west of the rutabaga / frog — opening kitchen dirt
    // west of the bath, not the yellow globe and not the
    // ivory globe. 10.12 is World CurbEntry between the fern
    // (9.95) and the beet (10.28), east of the potato, west
    // of the rutabaga (10.48). 12.08 is GardenWalk between
    // the poppy (11.95) and the pot (12.20), east of the
    // turnip (11.82) / rabbit (11.70). 22.80 is CurbReturn
    // between the turtle (22.60) and the mushroom (23.00),
    // west of the rutabaga (23.20) / hopper (23.40) / fire
    // logs 32.5. Stem on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly KohlrabiPeg[] KohlrabiPegs =
    {
        new KohlrabiPeg { x = -12.96f, face = 1f },
        new KohlrabiPeg { x = 10.12f, face = -1f },
        new KohlrabiPeg { x = 12.08f, face = 1f },
        new KohlrabiPeg { x = 22.80f, face = -1f }
    };

    // Kohlrabis own the pale-green swollen STEM ON the
    // slab; leeks the blanched SPEAR; cabbages the WRAPPER,
    // so RIBBED upright stalks ON the slab with compound
    // leaflets at the crown still had no celery. −13.44
    // is Level1 between the cattail (−13.68) and the pot
    // (−13.20), west of the kohlrabi — opening kitchen
    // dirt west of the bath, not the swollen stem and not
    // the reed. 9.84 is World CurbEntry between the potato
    // (9.72) and the fern (9.95), west of the kohlrabi
    // (10.12). 12.44 is GardenWalk between the potato
    // (12.36) and the iris (12.52), east of the kohlrabi
    // (12.08) / pot (12.20). 22.30 is CurbReturn between
    // DrainDip (22) and the turtle (22.60), west of the
    // kohlrabi (22.80) / mushroom (23.00) / fire logs
    // 32.5. Stalks on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly CeleryPeg[] CeleryPegs =
    {
        new CeleryPeg { x = -13.44f, face = 1f },
        new CeleryPeg { x = 9.84f, face = -1f },
        new CeleryPeg { x = 12.44f, face = 1f },
        new CeleryPeg { x = 22.30f, face = -1f }
    };

    // Celeries own the RIBBED upright stalks ON the slab;
    // leeks the blanched SPEAR; kohlrabis the pale-green
    // swollen STEM, so a SWOLLEN PALE bulb ON the slab
    // with a dill-like feathery crown still had no fennel.
    // −14.78 is Level1 between the ivy (−14.50) and the
    // nettle (−15.05), west of the duck / celery — opening
    // kitchen dirt west of the cattail, not the ribbed
    // stalks and not the reed. 9.35 is World CurbEntry
    // between the violet (9.20) and the pot (9.50), west
    // of the potato / celery (9.84). 13.62 is GardenWalk
    // between the hopper (13.50) and the lily (13.75),
    // east of the garlic (13.32). 22.12 is CurbReturn
    // 0.10 inside the DrainDip curb (22), west of the
    // celery (22.30) / turtle (22.60) / fire logs 32.5.
    // Bulb on the slab, not the grove drip. YardAmbient
    // does the sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly FennelPeg[] FennelPegs =
    {
        new FennelPeg { x = -14.78f, face = 1f },
        new FennelPeg { x = 9.35f, face = -1f },
        new FennelPeg { x = 13.62f, face = 1f },
        new FennelPeg { x = 22.12f, face = -1f }
    };

    // Fennels own the SWOLLEN PALE bulb ON the slab;
    // leeks the blanched SPEAR; celeries the RIBBED
    // stalks, so THIN GREEN spears from a SCALY crown
    // ON the slab still had no asparagus. −15.32 is
    // Level1 between the fern (−15.60) and the nettle
    // (−15.05), west of the fennel — opening kitchen dirt
    // west of the ivy, not the pale bulb and not the
    // pinna. 8.24 is World CurbEntry between the onion
    // (8.12) and the lantern (8.35), west of the fennel /
    // garlic (8.52). 14.12 is GardenWalk between the
    // dandelion (14.00) and the sunflower (14.25), east
    // of the fennel (13.62) / onion (13.88). 22.45 is
    // CurbReturn between celery (22.30) and the turtle
    // (22.60), east of fennel (22.12) / fire logs 32.5.
    // Spears on the slab, not the grove drip. YardAmbient
    // does the sway. Chunk seams at 16 m; stay 0.10
    // inside.
    public static readonly AsparagusPeg[] AsparagusPegs =
    {
        new AsparagusPeg { x = -15.32f, face = 1f },
        new AsparagusPeg { x = 8.24f, face = -1f },
        new AsparagusPeg { x = 14.12f, face = 1f },
        new AsparagusPeg { x = 22.45f, face = -1f }
    };

    // Asparagus owns the THIN GREEN spears ON the slab;
    // leeks the blanched SPEAR; fennels the SWOLLEN PALE
    // bulb, so THICK RED stalks with a HUGE wrinkled leaf
    // ON the slab still had no rhubarb. −15.88 is Level1
    // west of the fern (−15.60), west of asparagus —
    // opening kitchen dirt west of the nettle, not the
    // thin spears and not the pinna. 7.94 is World spawn
    // +X between the cattail (7.76) and the onion (8.12),
    // west of asparagus / lantern (8.35). 14.38 is
    // GardenWalk between the sunflower (14.25) and the
    // bath (14.50), east of asparagus (14.12). 22.70 is
    // CurbReturn between the turtle (22.60) and the
    // kohlrabi (22.80), east of asparagus (22.45) / fire
    // logs 32.5. Stalks on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly RhubarbPeg[] RhubarbPegs =
    {
        new RhubarbPeg { x = -15.88f, face = 1f },
        new RhubarbPeg { x = 7.94f, face = -1f },
        new RhubarbPeg { x = 14.38f, face = 1f },
        new RhubarbPeg { x = 22.70f, face = -1f }
    };

    // Rhubarbs own the THICK RED stalks ON the slab;
    // cabbage the ROUND WRAPPER head; asparagus the
    // THIN GREEN spears, so CORRUGATED BLUE-GREEN
    // standing leaves in a ROSETTE ON the slab still
    // had no kale. −16.44 is Level1 west of rhubarb
    // (−15.88), east of the web bay (−17.00) — opening
    // kitchen dirt west of the fern, not the red
    // petioles and not the pinna. 7.10 is World spawn
    // +X between the dock (7.00) and the duck (7.20),
    // west of rhubarb / cattail (7.76). 16.29 is
    // GardenWalk between the cabbage (16.18) and the
    // dock (16.40), east of rhubarb / bath (14.50) —
    // brassica next to the round head, not a wrap.
    // 22.90 is CurbReturn between the kohlrabi (22.80)
    // and the mushroom (23.00), east of rhubarb (22.70)
    // / fire logs 32.5. Rosette on the slab, not the
    // grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly KalePeg[] KalePegs =
    {
        new KalePeg { x = -16.44f, face = 1f },
        new KalePeg { x = 7.10f, face = -1f },
        new KalePeg { x = 16.29f, face = 1f },
        new KalePeg { x = 22.90f, face = -1f }
    };

    // Kales own the CORRUGATED BLUE-GREEN rosette ON
    // the slab; rhubarb the THICK RED stalks; cabbage
    // the ROUND WRAPPER, so BRIGHT stalks with a LOBED
    // leaf ON the slab still had no chard. −17.56 is
    // Level1 west of kale (−16.44), west of the web bay
    // (−17.00) — opening kitchen dirt west of the
    // standing ruffles, not the silk and not the teal
    // rosette. 6.54 is World spawn +X between the
    // bluebell (6.42) and the bath (6.65), west of kale
    // / leek (6.84) / dock (7.00). 16.69 is GardenWalk
    // between the pumpkin (16.58) and the pot (16.80),
    // east of kale (16.29) / cabbage (16.18) — bright
    // stalks next to the brassica, not a wrap and not a
    // gourd. 23.10 is CurbReturn between the mushroom
    // (23.00) and the rutabaga (23.20), east of kale
    // (22.90) / fire logs 32.5. Stalks on the slab, not
    // the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly ChardPeg[] ChardPegs =
    {
        new ChardPeg { x = -17.56f, face = 1f },
        new ChardPeg { x = 6.54f, face = -1f },
        new ChardPeg { x = 16.69f, face = 1f },
        new ChardPeg { x = 23.10f, face = -1f }
    };

    // Chards own the BRIGHT stalks with a LOBED leaf ON
    // the slab; kale the CORRUGATED BLUE-GREEN standing
    // rosette; cabbage the ROUND WRAPPER, so SMOOTH
    // DARK-GREEN leaves in a LOW rosette ON the slab
    // still had no spinach. −18.22 is Level1 west of
    // chard (−17.56), west of the web bay (−17.00) —
    // opening kitchen dirt east of the middle-fence
    // post (−19.50), not the bright paddles and not the
    // silk. 7.30 is World spawn +X between the duck
    // (7.20) and the worm (7.40), east of kale (7.10) /
    // chard (6.54). 17.10 is GardenWalk between the corn
    // (17.00) and the nettle (17.20), east of chard
    // (16.69) / pot (16.80) — dark flats next to the
    // tassel, not a wrap and not a sting. 23.30 is
    // CurbReturn between the rutabaga (23.20) and the
    // hopper (23.40), east of chard (23.10) / fire logs
    // 32.5. Rosette on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly SpinachPeg[] SpinachPegs =
    {
        new SpinachPeg { x = -18.22f, face = 1f },
        new SpinachPeg { x = 7.30f, face = -1f },
        new SpinachPeg { x = 17.10f, face = 1f },
        new SpinachPeg { x = 23.30f, face = -1f }
    };

    // Spinaches own the SMOOTH DARK-GREEN LOW rosette ON
    // the slab; cabbage the TIGHT ROUND WRAPPER; kale
    // the CORRUGATED BLUE-GREEN standing rosette, so a
    // LOOSE PALE-GREEN head ON the slab still had no
    // lettuce. −18.88 is Level1 west of spinach (−18.22),
    // east of the middle-fence post (−19.50) — opening
    // kitchen dirt under the high rail, not the dark
    // flats and not the post. 7.48 is World spawn +X
    // between the worm (7.40) and the bean (7.58), east
    // of spinach (7.30) / kale (7.10). 17.38 is
    // GardenWalk between the nettle (17.20) and the
    // turtle (17.55), east of spinach (17.10) / corn
    // (17.00) — a pale cup next to the sting, not a
    // wrap and not a dark pancake. 23.50 is CurbReturn
    // between the hopper (23.40) and the turnip (23.60),
    // east of spinach (23.30) / fire logs 32.5. Loose
    // head on the slab, not the grove drip. YardAmbient
    // does the sway. Chunk seams at 16 m; stay 0.10
    // inside.
    public static readonly LettucePeg[] LettucePegs =
    {
        new LettucePeg { x = -18.88f, face = 1f },
        new LettucePeg { x = 7.48f, face = -1f },
        new LettucePeg { x = 17.38f, face = 1f },
        new LettucePeg { x = 23.50f, face = -1f }
    };

    // Lettuces own the LOOSE PALE-GREEN cup ON the slab;
    // cabbage the TIGHT ROUND WRAPPER; spinach the
    // SMOOTH DARK-GREEN LOW rosette, so a TIGHT GREEN
    // floret head on a THICK stem ON the slab still had
    // no broccoli. −19.22 is Level1 west of lettuce
    // (−18.88), east of the middle-fence post (−19.50)
    // — opening kitchen dirt under the high rail, not
    // the pale cup and not the post. 7.68 is World
    // spawn +X between the bean (7.58) and the cattail
    // (7.76), east of lettuce (7.48) / spinach (7.30).
    // 17.72 is GardenWalk between the turtle (17.55)
    // and the cattail (17.90), east of lettuce (17.38) /
    // nettle (17.20) — a stem-head next to the wader,
    // not a wrap and not a pale cup. 23.70 is CurbReturn
    // between the turnip (23.60) and lavender (23.80),
    // east of lettuce (23.50) / hopper (23.40) / fire
    // logs 32.5. Floret on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly BroccoliPeg[] BroccoliPegs =
    {
        new BroccoliPeg { x = -19.22f, face = 1f },
        new BroccoliPeg { x = 7.68f, face = -1f },
        new BroccoliPeg { x = 17.72f, face = 1f },
        new BroccoliPeg { x = 23.70f, face = -1f }
    };

    // Broccolis own the TIGHT GREEN floret on a THICK
    // stem ON the slab; cabbage the TIGHT ROUND WRAPPER;
    // lettuce the LOOSE PALE-GREEN cup, so a TIGHT
    // CREAMY-WHITE curd on a THICK stem ON the slab
    // still had no cauliflower. −19.88 is Level1 west of
    // broccoli (−19.22), west of the middle-fence post
    // (−19.50) — opening kitchen dirt at the post foot
    // in the west bay, not the green floret and not the
    // post. 7.85 is World spawn +X between the cattail
    // (7.76) and the rhubarb (7.94), east of broccoli
    // (7.68) / bean (7.58). 17.81 is GardenWalk between
    // broccoli (17.72) and the cattail (17.90), east of
    // the turtle (17.55) / lettuce (17.38) — a cream
    // curd next to the green stem-head, not a wrap and
    // not a pale cup. 23.92 is CurbReturn between
    // lavender (23.80) and the parsnip (24.04), east of
    // broccoli (23.70) / turnip (23.60) / fire logs
    // 32.5. Curd on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly CauliflowerPeg[] CauliflowerPegs =
    {
        new CauliflowerPeg { x = -19.88f, face = 1f },
        new CauliflowerPeg { x = 7.85f, face = -1f },
        new CauliflowerPeg { x = 17.81f, face = 1f },
        new CauliflowerPeg { x = 23.92f, face = -1f }
    };

    // Cauliflowers own the TIGHT CREAMY-WHITE curd on a
    // THICK stem ON the slab; broccoli the TIGHT GREEN
    // floret dome; cabbage the TIGHT ROUND WRAPPER, so
    // TIGHT GREEN sprouts along a TALL stem ON the slab
    // still had no Brussels. −20.54 is Level1 west of
    // cauliflower (−19.88), west of the middle-fence
    // post (−19.50) — opening kitchen dirt in the west
    // bay, not the cream curd and not the post. 8.03 is
    // World CurbEntry between the rhubarb (7.94) and the
    // onion (8.12), east of cauliflower (7.85) / rhubarb.
    // 18.09 is DrainDip between the cattail (17.90) and
    // the heron (18.28), east of cauliflower (17.81) /
    // broccoli (17.72) — a sprout stalk next to the reed,
    // not a cream mound and not a green dome. 24.16 is
    // CurbReturn between the parsnip (24.04) and the
    // dandelion (24.28), east of cauliflower (23.92) /
    // parsnip / fire logs 32.5. Sprouts on the slab, not
    // the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly BrusselsPeg[] BrusselsPegs =
    {
        new BrusselsPeg { x = -20.54f, face = 1f },
        new BrusselsPeg { x = 8.03f, face = -1f },
        new BrusselsPeg { x = 18.09f, face = 1f },
        new BrusselsPeg { x = 24.16f, face = -1f }
    };

    // Brussels owns the TIGHT GREEN sprouts along a
    // TALL stem ON the slab; broccoli the TIGHT GREEN
    // floret dome; cauliflower the TIGHT CREAMY-WHITE
    // curd; thistle the PURPLE bloom on the dry verge,
    // so a SCALY edible bud on a THICK stem ON the slab
    // still had no artichoke. −21.20 is Level1 west of
    // Brussels (−20.54), west of the middle-fence post
    // (−19.50) — opening kitchen dirt in the west bay,
    // not the sprout stalk and not the post. 8.61 is
    // World CurbEntry between the garlic (8.52) and the
    // bluebell (8.70), east of Brussels (8.03) / onion
    // (8.12) / asparagus (8.24) / lantern (8.35) — the
    // first CurbEntry gap that fits a globe. 18.38 is
    // DrainDip between the heron (18.28) and the
    // strider (18.50), east of Brussels (18.09) /
    // cauliflower (17.81) — a scale bud on the bank,
    // not a sprout stalk and not a cream mound. 24.41
    // is CurbReturn between the dandelion (24.28) and
    // the beet (24.54), east of Brussels (24.16) /
    // dandelion / fire logs 32.5. Bud on the slab, not
    // the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly ArtichokePeg[] ArtichokePegs =
    {
        new ArtichokePeg { x = -21.20f, face = 1f },
        new ArtichokePeg { x = 8.61f, face = -1f },
        new ArtichokePeg { x = 18.38f, face = 1f },
        new ArtichokePeg { x = 24.41f, face = -1f }
    };

    // Artichoke owns the SCALY edible bud on a THICK
    // stem ON the slab; tomato the ROUND RED ball on a
    // stake vine; thistle the PURPLE bloom on the dry
    // verge, so a GLOSSY PURPLE teardrop fruit on a
    // stem ON the slab still had no eggplant. −21.86
    // is Level1 west of artichoke (−21.20), west of
    // the middle-fence post (−19.50) — opening kitchen
    // dirt in the west bay, not the scale bud and not
    // the post. 8.80 is World CurbEntry between the
    // bluebell (8.70) and the nettle (8.90), east of
    // artichoke (8.61) / garlic (8.52) — the next
    // CurbEntry gap that fits a hanging fruit. 21.85
    // is DrainDip between the heron (21.72) and the
    // CurbReturn curb (22), east of artichoke (18.38)
    // / duck (20.55) — a glossy fruit on the east
    // bank, not a scale bud and not a red ball. 24.67
    // is CurbReturn between the beet (24.54) and the
    // bramble (24.80), east of artichoke (24.41) /
    // beet / fire logs 32.5. Fruit on the slab, not
    // the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly EggplantPeg[] EggplantPegs =
    {
        new EggplantPeg { x = -21.86f, face = 1f },
        new EggplantPeg { x = 8.80f, face = -1f },
        new EggplantPeg { x = 21.85f, face = 1f },
        new EggplantPeg { x = 24.67f, face = -1f }
    };

    // Eggplant owns the GLOSSY PURPLE teardrop on a
    // stem ON the slab; tomato the ROUND RED ball on a
    // stake vine; artichoke the SCALY edible bud on a
    // THICK stem, so a GLOSSY BLOCKY pod ON the slab
    // still had no pepper. −22.52 is Level1 west of
    // eggplant (−21.86), west of the middle-fence post
    // (−19.50) — opening kitchen dirt in the west bay,
    // not the hanging teardrop and not the post. 8.97
    // is World CurbEntry between the nettle (8.90) and
    // the radish (9.05), east of eggplant (8.80) /
    // bluebell (8.70) — the next CurbEntry gap that fits
    // a blocky pod. 19.58 is DrainDip between the lily
    // (19.36) and the dragonfly (20.00), east of
    // artichoke (18.38) — a blocky pod on the mid bank,
    // not a hanging teardrop on the east bank (21.85)
    // and not a red ball. 24.95 is CurbReturn between
    // the bramble (24.80) and the birdhouse (25.15),
    // east of eggplant (24.67) / bramble / fire logs
    // 32.5. Pod on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly PepperPeg[] PepperPegs =
    {
        new PepperPeg { x = -22.52f, face = 1f },
        new PepperPeg { x = 8.97f, face = -1f },
        new PepperPeg { x = 19.58f, face = 1f },
        new PepperPeg { x = 24.95f, face = -1f }
    };

    // Pepper owns the GLOSSY BLOCKY pod on a stem ON
    // the slab; tomato the ROUND RED ball on a stake
    // vine; eggplant the GLOSSY PURPLE hanging
    // teardrop, so a LONG GREEN fruit on a vine ON the
    // slab still had no cucumber. −23.18 is Level1 west
    // of pepper (−22.52), west of the middle-fence post
    // (−19.50), east of the west fence post (−24.50) —
    // opening kitchen dirt in the west bay, not the
    // blocky pod and not the post. 9.12 is World
    // CurbEntry between the radish (9.05) and the
    // violet (9.20), east of pepper (8.97) / nettle
    // (8.90) — the next CurbEntry gap that fits a
    // sprawling vine. 20.28 is DrainDip between the
    // dragonfly / grate (20.00) and the duck (20.55),
    // east of pepper (19.58) — a long fruit on the
    // mid-east bank, not a blocky pod west of the grate
    // and not a hanging teardrop on the east bank
    // (21.85). 25.24 is CurbReturn between the
    // birdhouse (25.15) and the potato (25.32), east of
    // pepper (24.95) / birdhouse / fire logs 32.5.
    // Vine on the slab, not the grove drip. YardAmbient
    // does the sway. Chunk seams at 16 m; stay 0.10
    // inside.
    public static readonly CucumberPeg[] CucumberPegs =
    {
        new CucumberPeg { x = -23.18f, face = 1f },
        new CucumberPeg { x = 9.12f, face = -1f },
        new CucumberPeg { x = 20.28f, face = 1f },
        new CucumberPeg { x = 25.24f, face = -1f }
    };

    // Cucumber owns the LONG GREEN fruit on a vine ON
    // the slab; pumpkin the RIBBED gourd on a dirt
    // vine; pepper the GLOSSY BLOCKY pod, so a NETTED
    // ROUND fruit on a vine ON the slab still had no
    // melon. −23.84 is Level1 west of cucumber
    // (−23.18), west of the middle-fence post
    // (−19.50), east of the west fence post (−24.50)
    // — opening kitchen dirt in the west bay, not the
    // long green fruit and not the post. 9.28 is World
    // CurbEntry between the violet (9.20) and the
    // fennel (9.35), east of cucumber (9.12) / radish
    // (9.05) — the next CurbEntry gap that fits a
    // netted round fruit. 20.42 is DrainDip between
    // the cucumber (20.28) and the duck (20.55), east
    // of cucumber / dragonfly / grate (20.00) — a
    // round fruit on the mid-east bank, not a long
    // green fruit west of the duck and not a hanging
    // teardrop on the east bank (21.85). 25.41 is
    // CurbReturn between the potato (25.32) and the
    // violet (25.50), east of cucumber (25.24) /
    // birdhouse / fire logs 32.5. Vine on the slab,
    // not the grove drip. YardAmbient does the sway.
    // Chunk seams at 16 m; stay 0.10 inside.
    public static readonly MelonPeg[] MelonPegs =
    {
        new MelonPeg { x = -23.84f, face = 1f },
        new MelonPeg { x = 9.28f, face = -1f },
        new MelonPeg { x = 20.42f, face = 1f },
        new MelonPeg { x = 25.41f, face = -1f }
    };

    // Melon owns the NETTED ROUND fruit on a vine ON
    // the slab; cucumber the LONG GREEN fruit; pumpkin
    // the RIBBED gourd on a dirt vine, so a STRIPED
    // OVAL fruit on a vine ON the slab still had no
    // watermelon. −25.16 is Level1 west of the west
    // fence post (−24.50), west of melon (−23.84),
    // east of the web bay (−27.50) — opening kitchen
    // dirt in the far west bay, not the netted round
    // fruit east of the post and not the post itself.
    // 9.42 is World CurbEntry between the fennel (9.35)
    // and the pot (9.50), east of melon (9.28) /
    // violet (9.20) — the next CurbEntry gap that fits
    // a striped oval fruit. 20.70 is DrainDip east of
    // the duck (20.55), west of the minnow (21.10) /
    // lily (21.22), east of melon (20.42) — an oval
    // fruit on the mid-east bank, not a netted round
    // fruit west of the duck and not a hanging
    // teardrop on the east bank (21.85). 25.76 is
    // CurbReturn between the radish (25.68) and the
    // fern (25.85), east of melon (25.41) / violet
    // (25.50) / fire logs 32.5. Vine on the slab, not
    // the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly WatermelonPeg[] WatermelonPegs =
    {
        new WatermelonPeg { x = -25.16f, face = 1f },
        new WatermelonPeg { x = 9.42f, face = -1f },
        new WatermelonPeg { x = 20.70f, face = 1f },
        new WatermelonPeg { x = 25.76f, face = -1f }
    };

    // Watermelon owns the STRIPED OVAL fruit on a
    // vine ON the slab; melon the NETTED ROUND fruit;
    // pumpkin the RIBBED gourd on a dirt vine, so a
    // YELLOW HOOK fruit on a vine ON the slab still
    // had no zucchini. −25.82 is Level1 west of
    // watermelon (−25.16), west of the west fence
    // post (−24.50), east of the web bay (−27.50) —
    // opening kitchen dirt in the far west bay, not
    // the striped oval fruit and not the web. 9.58
    // is World CurbEntry between the pot (9.50) and
    // the potato (9.72), east of watermelon (9.42) /
    // fennel (9.35) — the next CurbEntry gap that fits
    // a yellow hook. 20.90 is DrainDip between
    // watermelon (20.70) and the minnow (21.10) /
    // lily (21.22), east of watermelon / duck —
    // a hook fruit on the mid-east bank, not a
    // striped oval west of the minnow and not a
    // hanging teardrop on the east bank (21.85).
    // 25.98 is CurbReturn between the fern (25.85)
    // and the crocus (26.12), east of watermelon
    // (25.76) / fern / fire logs 32.5. Vine on the
    // slab, not the grove drip. YardAmbient does the
    // sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly ZucchiniPeg[] ZucchiniPegs =
    {
        new ZucchiniPeg { x = -25.82f, face = 1f },
        new ZucchiniPeg { x = 9.58f, face = -1f },
        new ZucchiniPeg { x = 20.90f, face = 1f },
        new ZucchiniPeg { x = 25.98f, face = -1f }
    };

    // Zucchini owns the YELLOW HOOK fruit on a vine
    // ON the slab; watermelon the STRIPED OVAL; pumpkin
    // the RIBBED gourd on a dirt vine, so a YELLOW
    // SCALLOPED DISK on a vine ON the slab still had no
    // pattypan. −26.48 is Level1 west of zucchini
    // (−25.82), west of the west fence post (−24.50),
    // east of the web bay (−27.50) — opening kitchen
    // dirt in the far west bay, not the yellow hook and
    // not the web. 10.04 is World CurbEntry between the
    // fern (9.95) and the kohlrabi (10.12), east of
    // zucchini (9.58) / potato (9.72) / celery (9.84)
    // — the next CurbEntry gap that fits a scalloped
    // disk. 21.34 is DrainDip between the lily (21.22)
    // and the strider (21.45), east of zucchini (20.90)
    // / minnow (21.10) — a disk fruit on the mid-east
    // bank, not a yellow hook west of the minnow and
    // not a hanging teardrop on the east bank (21.85).
    // 26.26 is CurbReturn between the crocus (26.12)
    // and the thistle (26.40), east of zucchini (25.98)
    // / fern / fire logs 32.5. Vine on the slab, not
    // the grove drip. YardAmbient does the sway. Chunk
    // seams at 16 m; stay 0.10 inside.
    public static readonly PattypanPeg[] PattypanPegs =
    {
        new PattypanPeg { x = -26.48f, face = 1f },
        new PattypanPeg { x = 10.04f, face = -1f },
        new PattypanPeg { x = 21.34f, face = 1f },
        new PattypanPeg { x = 26.26f, face = -1f }
    };

    // Pattypan owns the YELLOW SCALLOPED DISK on a
    // vine ON the slab; zucchini the YELLOW HOOK;
    // pumpkin the RIBBED gourd on a dirt vine, so a
    // BEIGE BOTTLE fruit on a vine ON the slab still
    // had no butternut. −27.14 is Level1 west of
    // pattypan (−26.48), west of the west fence post
    // (−24.50), east of the web bay (−27.50) —
    // opening kitchen dirt in the far west bay, not
    // the scalloped disk and not the web. 10.20 is
    // World CurbEntry between the kohlrabi (10.12)
    // and the beet (10.28), east of pattypan (10.04)
    // / fern (9.95) — the next CurbEntry gap that fits
    // a bottle fruit. 21.58 is DrainDip between the
    // strider (21.45) and the heron (21.72), east of
    // pattypan (21.34) / lily (21.22) — a bottle fruit
    // on the mid-east bank, not a scalloped disk west
    // of the strider and not a hanging teardrop on
    // the east bank (21.85). 26.51 is CurbReturn
    // between the thistle (26.40) and the garlic
    // (26.62), east of pattypan (26.26) / fern / fire
    // logs 32.5. Vine on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at 16 m;
    // stay 0.10 inside.
    public static readonly ButternutPeg[] ButternutPegs =
    {
        new ButternutPeg { x = -27.14f, face = 1f },
        new ButternutPeg { x = 10.20f, face = -1f },
        new ButternutPeg { x = 21.58f, face = 1f },
        new ButternutPeg { x = 26.51f, face = -1f }
    };

    // Butternut owns the BEIGE BOTTLE fruit on a
    // vine ON the slab; pattypan the YELLOW SCALLOPED
    // DISK; pumpkin the RIBBED orange gourd on a dirt
    // vine, so a DARK-GREEN RIBBED ACORN fruit on a
    // vine ON the slab still had no acorn. −27.80 is
    // Level1 west of butternut (−27.14), west of the
    // web bay (−27.50), east of the west nest (−29.85)
    // / west post (−30.50) — opening kitchen dirt in
    // the far west bay past the silk, not the bottle
    // and not the web. 10.36 is World CurbEntry
    // between the beet (10.28) and the rutabaga
    // (10.48), east of butternut (10.20) / kohlrabi
    // — the next CurbEntry gap that fits a heart
    // gourd. 21.65 is DrainDip between the butternut
    // (21.58) and the heron (21.72), east of pattypan
    // (21.34) / lily — an acorn fruit on the mid-east
    // bank, not a beige bottle west of the heron and
    // not a hanging teardrop on the east bank (21.85).
    // 26.74 is CurbReturn between the garlic (26.62)
    // and the hollyhock (26.85), east of butternut
    // (26.51) / thistle / fire logs 32.5. Vine on the
    // slab, not the grove drip. YardAmbient does the
    // sway. Chunk seams at 16 m; stay 0.10 inside.
    public static readonly AcornPeg[] AcornPegs =
    {
        new AcornPeg { x = -27.80f, face = 1f },
        new AcornPeg { x = 10.36f, face = -1f },
        new AcornPeg { x = 21.65f, face = 1f },
        new AcornPeg { x = 26.74f, face = -1f }
    };

    // Acorn owns the DARK-GREEN RIBBED ACORN fruit on
    // a vine ON the slab; butternut the BEIGE BOTTLE;
    // pumpkin the RIBBED orange gourd on a dirt vine,
    // so a PALE-YELLOW OBLONG fruit on a vine ON the
    // slab still had no spaghetti squash. −28.46 is
    // Level1 west of acorn (−27.80), west of the web
    // bay (−27.50), east of the west nest (−29.85) /
    // west post (−30.50) — opening kitchen dirt in the
    // far west bay past the heart gourd, not the acorn
    // and not the nest. 10.56 is World CurbEntry
    // between the rutabaga (10.48) and the east entry
    // lantern / post (10.65), east of acorn (10.36) /
    // beet — the next CurbEntry gap that fits an oblong
    // squash. 21.78 is DrainDip between the heron (21.72)
    // and the eggplant (21.85), east of acorn (21.65) /
    // lily — a pale oval on the mid-east bank, not a
    // dark-green heart west of the heron and not a
    // hanging teardrop on the east bank (21.85). 27.12
    // is CurbReturn between the hollyhock (26.85) and
    // the rose (27.42), east of acorn (26.74) / garlic /
    // fire logs 32.5. Vine on the slab, not the grove
    // drip. YardAmbient does the sway. Chunk seams at
    // 16 m; stay 0.10 inside.
    public static readonly SpaghettiPeg[] SpaghettiPegs =
    {
        new SpaghettiPeg { x = -28.46f, face = 1f },
        new SpaghettiPeg { x = 10.56f, face = -1f },
        new SpaghettiPeg { x = 21.78f, face = 1f },
        new SpaghettiPeg { x = 27.12f, face = -1f }
    };

    // Spaghetti owns the PALE-YELLOW OBLONG fruit on
    // a vine ON the slab; acorn the DARK-GREEN RIBBED
    // ACORN; butternut the BEIGE BOTTLE, so a CREAM
    // OBLONG with DARK-GREEN STRIPES on a vine ON the
    // slab still had no delicata. −29.12 is Level1
    // west of spaghetti (−28.46), west of the web bay
    // (−27.50), east of the west nest (−29.85) / west
    // post (−30.50) — opening kitchen dirt in the far
    // west bay past the pale oval, not the spaghetti
    // and not the nest. 10.93 is World CurbEntry
    // between the parsnip (10.82) and the turnip
    // (11.04) / GardenWalk curb, east of spaghetti
    // (10.56) / the east entry lantern (10.65) — the
    // next CurbEntry gap that fits a striped cream
    // fruit. 21.96 is DrainDip between the eggplant
    // (21.85) and the CurbReturn curb (22), east of
    // spaghetti (21.78) / heron — a cream stripe on
    // the east bank, not a pale oval west of the
    // eggplant and not a hanging teardrop on the east
    // bank (21.85). 27.27 is CurbReturn between
    // spaghetti (27.12) and the rose (27.42), east of
    // spaghetti / hollyhock / fire logs 32.5. Vine
    // on the slab, not the grove drip. YardAmbient
    // does the sway. Chunk seams at 16 m; stay 0.10
    // inside.
    public static readonly DelicataPeg[] DelicataPegs =
    {
        new DelicataPeg { x = -29.12f, face = 1f },
        new DelicataPeg { x = 10.93f, face = -1f },
        new DelicataPeg { x = 21.96f, face = 1f },
        new DelicataPeg { x = 27.27f, face = -1f }
    };

    // Delicata owns the CREAM OBLONG with DARK-GREEN
    // STRIPES on a vine ON the slab; spaghetti the
    // PALE-YELLOW OBLONG; acorn the DARK-GREEN
    // RIBBED ACORN, so a ROUND CREAM globe with
    // ORANGE-GREEN MOTTLE on a vine ON the slab
    // still had no carnival. −29.78 is Level1 west
    // of delicata (−29.12), west of the web bay
    // (−27.50), east of the west post (−30.50) —
    // opening kitchen dirt under the west-nest bay
    // (−29.85 sits the high rail, not the slab),
    // not the cream stripe and not the post. 11.12
    // is World GardenWalk between the turnip (11.04)
    // and the tulip (11.20), east of delicata
    // (10.93) / GardenWalk curb — the first
    // GardenWalk gap that fits a round cream fruit.
    // 22.04 is CurbReturn just inside the DrainDip
    // curb (22), west of fennel (22.12), east of
    // delicata (21.96) / heron — a cream globe over
    // the curb, not a striped oblong on the east
    // bank. 27.52 is CurbReturn between the rose
    // (27.42) and the onion (27.64), east of
    // delicata (27.27) / rose / fire logs 32.5.
    // Vine on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at
    // 16 m; stay 0.10 inside.
    public static readonly CarnivalPeg[] CarnivalPegs =
    {
        new CarnivalPeg { x = -29.78f, face = 1f },
        new CarnivalPeg { x = 11.12f, face = -1f },
        new CarnivalPeg { x = 22.04f, face = 1f },
        new CarnivalPeg { x = 27.52f, face = -1f }
    };

    // Carnival owns the ROUND CREAM globe with
    // ORANGE-GREEN MOTTLE on a vine ON the slab;
    // delicata the CREAM OBLONG with DARK-GREEN
    // STRIPES; spaghetti the PALE-YELLOW OBLONG, so
    // a SMALL ROUND CREAM fruit with DARK-GREEN RIBS
    // on a vine ON the slab still had no sweet
    // dumpling. −30.22 is Level1 west of carnival
    // (−29.78), west of the web bay (−27.50), east
    // of the west post (−30.50) — opening kitchen
    // dirt toward the post under the west-nest bay
    // (−29.85 sits the high rail, not the slab),
    // not the cream globe and not the post. 11.26
    // is World GardenWalk between the tulip (11.20)
    // and the beet (11.32), east of carnival
    // (11.12) / GardenWalk curb — the next
    // GardenWalk gap that fits a small ribbed cream
    // fruit. 22.20 is CurbReturn between fennel
    // (22.12) and celery (22.30), east of carnival
    // (22.04) / DrainDip curb — a small cream globe
    // on the curb, not a mottled globe just inside
    // the dip. 27.74 is CurbReturn between the
    // onion (27.64) and the leek (27.85), east of
    // carnival (27.52) / onion / fire logs 32.5.
    // Vine on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at
    // 16 m; stay 0.10 inside.
    public static readonly SweetDumplingPeg[] SweetDumplingPegs =
    {
        new SweetDumplingPeg { x = -30.22f, face = 1f },
        new SweetDumplingPeg { x = 11.26f, face = -1f },
        new SweetDumplingPeg { x = 22.20f, face = 1f },
        new SweetDumplingPeg { x = 27.74f, face = -1f }
    };

    // Sweet dumpling owns the SMALL ROUND CREAM
    // fruit with DARK-GREEN RIBS on a vine ON the
    // slab; carnival the ROUND CREAM globe with
    // ORANGE-GREEN MOTTLE; delicata the CREAM
    // OBLONG with DARK-GREEN STRIPES, so a SQUAT
    // DARK-GREEN globe with PALE SPECKLES on a vine
    // ON the slab still had no kabocha. −30.90 is
    // Level1 west of sweet dumpling (−30.22), west
    // of the west post (−30.50), west of the web bay
    // (−27.50) — opening kitchen dirt past the post
    // under the west-nest bay (−29.85 sits the high
    // rail, not the slab), not the cream ribbed
    // globe and not the post. 11.40 is World
    // GardenWalk between the beet (11.32) and the
    // daisy (11.45), east of sweet dumpling (11.26)
    // / GardenWalk curb — the next GardenWalk gap
    // that fits a squat dark-green fruit. 22.38 is
    // CurbReturn between celery (22.30) and
    // asparagus (22.45), east of sweet dumpling
    // (22.20) / DrainDip curb — a squat green globe
    // on the curb, not a small cream globe between
    // fennel and celery. 27.96 is CurbReturn between
    // the leek (27.85) and the bean (28.06), east of
    // sweet dumpling (27.74) / leek / fire logs
    // 32.5. Vine on the slab, not the grove drip.
    // YardAmbient does the sway. Chunk seams at
    // 16 m; stay 0.10 inside.
    public static readonly KabochaPeg[] KabochaPegs =
    {
        new KabochaPeg { x = -30.90f, face = 1f },
        new KabochaPeg { x = 11.40f, face = -1f },
        new KabochaPeg { x = 22.38f, face = 1f },
        new KabochaPeg { x = 27.96f, face = -1f }
    };

    private static readonly Color PostTint = new Color(0.28f, 0.18f, 0.10f, 1f);
    private static readonly Color BenchTint = new Color(0.42f, 0.27f, 0.14f, 1f);
    private static readonly Color GrateTint = new Color(0.20f, 0.22f, 0.23f, 1f);
    private static readonly Color LookoutTint = new Color(0.52f, 0.39f, 0.20f, 1f);
    private static readonly Color CurbFaceTint = new Color(0.34f, 0.22f, 0.12f, 1f);
    private static readonly Color DrainFaceTint = new Color(0.18f, 0.22f, 0.23f, 1f);
    private static readonly Color LookoutFaceTint = new Color(0.43f, 0.41f, 0.36f, 1f);
    private static readonly Color FencePostTint = new Color(0.24f, 0.15f, 0.08f, 1f);
    private static readonly Color FenceRailTint = new Color(0.38f, 0.25f, 0.14f, 1f);
    private static readonly Vector2 CurbFaceSize = new Vector2(0.14f, 0.05f);
    private static readonly Vector2 FencePostSize = new Vector2(0.10f, 0.62f);

    // Picture only. A collider here would be a wall for the feet.
    private static readonly Decor[] Marks =
    {
        new Decor { name = "SpawnLampPost", x = 3.25f, centerY = -1.46f, size = new Vector2(0.07f, 1.08f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "EntryPostWest", x = 8.35f, centerY = -1.79f, size = new Vector2(0.07f, 0.32f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "EntryPostEast", x = 10.65f, centerY = -1.79f, size = new Vector2(0.07f, 0.32f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "DrainGrate", x = 20f, centerY = -1.975f, size = new Vector2(1.20f, 0.05f), color = GrateTint, kind = LevelArt.FillKey },
        new Decor { name = "SquareBenchSeat", x = 34f, centerY = -1.41f, size = new Vector2(1.80f, 0.14f), color = BenchTint, kind = LevelArt.FillKey },
        new Decor { name = "SquareBenchLegL", x = 33.35f, centerY = -1.69f, size = new Vector2(0.10f, 0.42f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "SquareBenchLegR", x = 34.65f, centerY = -1.69f, size = new Vector2(0.10f, 0.42f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "GatePostWest", x = 40.25f, centerY = -1.63f, size = new Vector2(0.08f, 0.64f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "GatePostEast", x = 41.75f, centerY = -1.63f, size = new Vector2(0.08f, 0.64f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "FinishSign", x = SignX, centerY = -1.45f, size = new Vector2(0.18f, 0.90f), color = LevelArt.SignTint(), kind = LevelArt.SignKey },
        new Decor { name = "RainGutterGrate", x = 58f, centerY = -1.875f, size = new Vector2(1.60f, 0.05f), color = GrateTint, kind = LevelArt.FillKey },
        new Decor { name = "OrchardBenchSeat", x = 72f, centerY = -1.41f, size = new Vector2(1.50f, 0.14f), color = BenchTint, kind = LevelArt.FillKey },
        new Decor { name = "OrchardBenchLegL", x = 71.45f, centerY = -1.69f, size = new Vector2(0.10f, 0.42f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "OrchardBenchLegR", x = 72.55f, centerY = -1.69f, size = new Vector2(0.10f, 0.42f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "LookoutMarker", x = 93f, centerY = -1.50f, size = new Vector2(0.09f, 0.60f), color = LookoutTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceWestPostL", x = -30.50f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceWestPostR", x = -24.50f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceWestRailLow", x = -27.50f, centerY = -1.82f, size = new Vector2(5.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceWestRailHigh", x = -27.50f, centerY = -1.55f, size = new Vector2(5.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddlePostL", x = -19.50f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddlePostR", x = -14.50f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddleRailLow", x = -17f, centerY = -1.82f, size = new Vector2(4.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddleRailHighL", x = -18.325f, centerY = -1.55f, size = new Vector2(2.25f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddleRailHighR", x = -15.675f, centerY = -1.55f, size = new Vector2(2.25f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceEastPostL", x = -9f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceEastPostR", x = -3f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceEastRailLow", x = -6f, centerY = -1.82f, size = new Vector2(5.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceEastRailHigh", x = -6f, centerY = -1.55f, size = new Vector2(5.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceEntryRise", x = 8f, centerY = -1.975f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceDrainWest", x = 18f, centerY = -1.975f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceDrainEast", x = 22f, centerY = -1.975f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceSquareRise", x = 28f, centerY = -1.925f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceGateWest", x = 40f, centerY = -1.925f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceGateEast", x = 42f, centerY = -1.925f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceEastRise", x = 50f, centerY = -1.875f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceRainWest", x = 56f, centerY = -1.875f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceRainEast", x = 60f, centerY = -1.925f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceOrchardRise", x = 66f, centerY = -1.925f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceUpperRise", x = 76f, centerY = -1.875f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceUpperDrop", x = 80f, centerY = -1.875f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceLookoutApproach", x = 88f, centerY = -1.875f, size = CurbFaceSize, color = LookoutFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceLookoutRise", x = 92f, centerY = -1.825f, size = CurbFaceSize, color = LookoutFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceLookoutExit", x = 94f, centerY = -1.825f, size = CurbFaceSize, color = LookoutFaceTint, kind = LevelArt.FillKey }
    };

    public static List<LevelPiece> PiecesForChunk(int chunkIndex, float chunkWidth)
    {
        float x0 = chunkIndex * chunkWidth;
        float x1 = x0 + chunkWidth;
        var pieces = new List<LevelPiece>(8);

        for (int i = 0; i < Slabs.Length; i++)
        {
            Slab slab = Slabs[i];
            float a = Mathf.Max(slab.left, x0);
            float b = Mathf.Min(slab.left + slab.width, x1);
            if (b - a < 0.02f)
                continue;
            pieces.Add(MakeSlab(a, b - a, slab.surfaceY, slab.name));
        }

        if (pieces.Count == 0)
        {
            float y = chunkIndex < 0
                ? LevelGenerator.DefaultSurfaceY
                : Slabs[Slabs.Length - 1].surfaceY;
            pieces.Add(MakeSlab(x0, chunkWidth, y, "Extend"));
        }

        return pieces;
    }

    public static void SpawnDecor(int chunkIndex, float chunkWidth, Transform parent)
    {
        float x0 = chunkIndex * chunkWidth;
        float x1 = x0 + chunkWidth;

        for (int i = 0; i < Marks.Length; i++)
        {
            Decor mark = Marks[i];
            if (mark.x < x0 || mark.x >= x1)
                continue;
            StaticPlatform.Spawn(new LevelPiece
            {
                name = mark.name,
                center = new Vector2(mark.x, mark.centerY),
                size = mark.size,
                rotationZ = 0f,
                color = mark.color,
                kind = mark.kind,
                hasCollider = false
            }, parent);
        }

        YardAmbient.PlantChunk(chunkIndex, chunkWidth, parent);
    }

    // DrainDip / RainGutter: standing water picture, not a collider.
    public static bool IsWetAt(float x)
    {
        for (int i = 0; i < Slabs.Length; i++)
        {
            Slab slab = Slabs[i];
            if (x < slab.left || x >= slab.left + slab.width)
                continue;
            return slab.name == "DrainDip" || slab.name == "RainGutter";
        }

        return false;
    }

    // Picture tufts skip benches / posts so they do not sit on furniture.
    public static bool NearDecor(float x, float extra)
    {
        for (int i = 0; i < Marks.Length; i++)
        {
            float half = Marks[i].size.x * 0.5f + extra;
            float d = x - Marks[i].x;
            if (d * d < half * half)
                return true;
        }

        return false;
    }

    public static float SurfaceAt(float x)
    {
        for (int i = 0; i < Slabs.Length; i++)
        {
            Slab slab = Slabs[i];
            if (x >= slab.left && x < slab.left + slab.width)
                return slab.surfaceY;
        }

        return x < Slabs[0].left ? Slabs[0].surfaceY : Slabs[Slabs.Length - 1].surfaceY;
    }

    private static LevelPiece MakeSlab(float leftX, float width, float surfaceY, string name)
    {
        return new LevelPiece
        {
            name = name,
            center = new Vector2(leftX + width * 0.5f, surfaceY - LevelGenerator.GroundThickness * 0.5f),
            size = new Vector2(width, LevelGenerator.GroundThickness),
            rotationZ = 0f,
            color = SlabTint(name),
            kind = LevelArt.FillKey,
            hasCollider = true
        };
    }

    private static Color SlabTint(string name)
    {
        Color baseTint = LevelArt.FillTint();
        if (name != null && name.StartsWith("Curb"))
            return baseTint * new Color(0.86f, 0.86f, 0.86f, 1f);
        if (name == "DrainDip" || name == "RainGutter")
            return baseTint * new Color(0.78f, 0.80f, 0.82f, 1f);
        return baseTint;
    }
}
