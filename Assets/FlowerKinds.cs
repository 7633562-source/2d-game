using UnityEngine;

// One Add-Component type per garden flower. Each only locks FlowerKind.
// Build stays on PlantFlower so recipes do not fork.

public class FlowerRose : PlantFlower
{
    void Reset() { kind = FlowerKind.Rose; }
    void Awake() { kind = FlowerKind.Rose; }
}

public class FlowerTulip : PlantFlower
{
    void Reset() { kind = FlowerKind.Tulip; }
    void Awake() { kind = FlowerKind.Tulip; }
}

public class FlowerDaisy : PlantFlower
{
    void Reset() { kind = FlowerKind.Daisy; }
    void Awake() { kind = FlowerKind.Daisy; }
}

public class FlowerPoppy : PlantFlower
{
    void Reset() { kind = FlowerKind.Poppy; }
    void Awake() { kind = FlowerKind.Poppy; }
}

public class FlowerSunflower : PlantFlower
{
    void Reset() { kind = FlowerKind.Sunflower; }
    void Awake() { kind = FlowerKind.Sunflower; }
}

public class FlowerLily : PlantFlower
{
    void Reset() { kind = FlowerKind.Lily; }
    void Awake() { kind = FlowerKind.Lily; }
}

public class FlowerDaffodil : PlantFlower
{
    void Reset() { kind = FlowerKind.Daffodil; }
    void Awake() { kind = FlowerKind.Daffodil; }
}

public class FlowerViolet : PlantFlower
{
    void Reset() { kind = FlowerKind.Violet; }
    void Awake() { kind = FlowerKind.Violet; }
}

public class FlowerCrocus : PlantFlower
{
    void Reset() { kind = FlowerKind.Crocus; }
    void Awake() { kind = FlowerKind.Crocus; }
}

public class FlowerLavender : PlantFlower
{
    void Reset() { kind = FlowerKind.Lavender; }
    void Awake() { kind = FlowerKind.Lavender; }
}

public class FlowerIris : PlantFlower
{
    void Reset() { kind = FlowerKind.Iris; }
    void Awake() { kind = FlowerKind.Iris; }
}

public class FlowerBluebell : PlantFlower
{
    void Reset() { kind = FlowerKind.Bluebell; }
    void Awake() { kind = FlowerKind.Bluebell; }
}
