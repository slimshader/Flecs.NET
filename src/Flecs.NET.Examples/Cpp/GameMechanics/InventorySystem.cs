// This example shows one possible way to implement an inventory system using
// ECS relationships.

#if Cpp_GameMechanics_InventorySystem

using System.Runtime.CompilerServices;
using Flecs.NET.Core;

// Find Item kind of entity
Entity ItemKind(Entity item)
{
    World world = item.CsWorld();
    Entity result = default;

    item.Each((Id id) =>
    {
        if (id.IsEntity())
        {
            // If id is a plain entity (component), check if component inherits
            // from Item
            if (id.Entity().Has(Ecs.IsA, world.Id<Item>()))
                result = id.Entity();
        }
        else if (id.IsPair())
        {
            // If item has a base entity, check if the base has an attribute
            // that is an Item.
            if (id.First() == Ecs.IsA)
            {
                Entity baseKind = ItemKind(id.Second());
                if (baseKind != 0)
                    result = baseKind;
            }
        }
    });

    return result;
}

// Almost the same as ItemKind, but return name of prefab vs item kind. This
// returns a more user-friendly name, like "WoodenSword" vs. just "Sword"
string ItemName(Entity item)
{
    World world = item.CsWorld();
    string result = "";

    item.Each((Id id) =>
    {
        if (id.IsEntity())
        {
            if (id.Entity().Has(Ecs.IsA, world.Id<Item>()))
                result = id.Entity().Name();
        }
        else if (id.IsPair())
        {
            if (id.First() == Ecs.IsA)
            {
                Entity baseKind = ItemKind(id.Second());
                if (baseKind != 0)
                    result = id.Second().Name();
            }
        }
    });

    return result;
}

// If entity is not a container, get its inventory
Entity GetContainer(Entity container)
{
    return container.Has<Container>() ? container : container.Target<Inventory>();
}

// Iterate all items in an inventory
void ForEachItem(Entity container, Ecs.EachEntityCallback func)
{
    World world = container.CsWorld();

    using Filter filter = container.CsWorld().Filter(
        filter: world.FilterBuilder().With<ContainedBy>(container)
    );

    filter.Each(func);
}

// Find item in inventory of specified kind
Entity FindItemWithKind(Entity container, Entity kind, bool activeRequired = false)
{
    Entity result = default;

    container = GetContainer(container);

    ForEachItem(container, (Entity item) =>
    {
        // Check if we should only return active items. This is useful when
        // searching for an item that needs to be equipped.
        if (activeRequired)
        {
            if (!item.Has<Active>())
                return;
        }

        Entity ik = ItemKind(item);

        if (ik == kind)
            result = item;
    });

    return result;
}

// Transfer item to container
void TransferItem(Entity container, Entity item)
{
    ref readonly Amount amt = ref item.Get<Amount>();

    if (!Macros.IsNullReadOnlyRef(amt))
    {
        // If item has amount we need to check if the container already has an
        // item of this kind, and increase the value.
        World ecs = container.CsWorld();
        Entity ik = ItemKind(item);
        Entity dstItem = FindItemWithKind(container, ik);

        if (dstItem != 0)
        {
            // If a matching item was found, increase its amount
            ref Amount dstAmt = ref dstItem.GetMut<Amount>();
            dstAmt.Value += amt.Value;
            item.Destruct(); // Remove the src item
            return;
        }
        else
        {
            // If no matching item was found, fallthrough which will move the
            // item from the src container to the dst container
        }
    }

    // Move item to target container (replaces previous ContainedBy, if any)
    item.Add<ContainedBy>(container);
}

// Move items from one container to another
void TransferItems(Entity dst, Entity src)
{
    Console.WriteLine($">> Transfer items from {src} to {dst}\n");

    // Defer, because we're adding/removing components while we're iterating
    dst.CsWorld().Defer(() =>
    {
        dst = GetContainer(dst); // Make sure to replace players with container
        src = GetContainer(src);

        ForEachItem(src, (Entity item) =>
        {
            TransferItem(dst, item);
        });
    });
}

// Attack player
void Attack(Entity player, Entity weapon)
{
    World ecs = player.CsWorld();

    Console.WriteLine($">> {player} is attacked with a {ItemName(weapon)}!");

    ref readonly Attack att = ref weapon.Get<Attack>();

    if (Macros.IsNullReadOnlyRef(att))
    {
        // A weapon without Attack power? Odd.
        Console.WriteLine(" - the weapon is a dud");
        return;
    }

    int attValue = att.Value;

    // Get armor item, if player has equipped any
    Entity armor = FindItemWithKind(player, ecs.Entity<Armor>(), true);

    if (armor != 0)
    {
        ref Health armorHealth = ref armor.GetMut<Health>();

        if (Unsafe.IsNullRef(ref armorHealth))
        {
            // Armor without Defense power? Odd.
            Console.WriteLine($" - the {ItemName(armor)} armor is a dud");
        }
        else
        {
            Console.WriteLine($" - {player} defends with {ItemName(armor)}");

            // Subtract attack from armor health. If armor health goes below
            // zero, delete the armor and carry over remaining attack points.
            armorHealth.Value -= attValue;
            if (armorHealth.Value <= 0)
            {
                attValue += armorHealth.Value;
                armor.Destruct();
                Console.WriteLine($" - {ItemName(armor)} is destroyed!");
            }
            else
            {
                Console.WriteLine(
                    $" - {ItemName(armor)} has {armorHealth.Value} health left after taking {attValue} damage");

                attValue = 0;
            }
        }
    }
    else
    {
        // Brave but stupid
        Console.WriteLine($" - {player} fights without armor!");
    }

    // For each usage of the weapon, subtract one from its health
    ref Health weaponHealth = ref weapon.GetMut<Health>();

    if (--weaponHealth.Value == 0)
    {
        Console.WriteLine($" - {ItemName(weapon)} is destroyed!");
        weapon.Destruct();
    }
    else
    {
        Console.WriteLine($" - {ItemName(weapon)} has {weaponHealth.Value} uses left");
    }

    // If armor didn't counter the whole attack, subtract from the player health
    if (attValue != 0)
    {
        ref Health playerHealth = ref player.GetMut<Health>();

        if ((playerHealth.Value -= attValue) == 0)
        {
            Console.WriteLine($" - {player} died!");
            player.Destruct();
        }
        else
        {
            Console.WriteLine($" - {player} has {playerHealth.Value} health after taking {attValue} damage");
        }
    }

    Console.WriteLine();
}

// Print items in inventory
void PrintItems(Entity container)
{
    Console.WriteLine($"-- {container}'s inventory:");

    // In case the player entity was provided, make sure we're working with its
    // inventory entity.
    container = GetContainer(container);

    int count = 0;
    ForEachItem(container, (Entity item) =>
    {
        // Items with an Amount component fill up a single inventory slot but
        // represent multiple instances, like coins.
        int amount = 1;

        if (item.Has<Amount>())
            amount = item.Get<Amount>().Value;

        Console.Write($" - {amount} {ItemName(item)}");

        if (amount > 1)
            Console.Write("s");

        Console.WriteLine($" ({ItemKind(item)})");

        count ++;
    });

    if (count == 0)
        Console.WriteLine(" - << empty >>");

    Console.WriteLine();
}

using World ecs = World.Create();

// Register ContainedBy relationship
ecs.Component<ContainedBy>().Entity
    .Add(Ecs.Exclusive); // Item can only be contained by one container

// Register item kinds
ecs.Component<Sword>().Entity.IsA<Item>();
ecs.Component<Armor>().Entity.IsA<Item>();
ecs.Component<Coin>().Entity.IsA<Item>();

// Register item prefabs
ecs.Prefab<WoodenSword>().Add<Sword>()
    .Set(new Attack { Value = 1 })
    .SetOverride(new Health { Value = 5 }); // copy to instance, don't share

ecs.Prefab<IronSword>().Add<Sword>()
    .Set(new Attack { Value = 2 })
    .SetOverride(new Health { Value = 10 });

ecs.Prefab<WoodenArmor>().Add<Armor>()
    .SetOverride(new Health { Value = 10 });

ecs.Prefab<IronArmor>().Add<Armor>()
    .SetOverride(new Health { Value = 20 });

// Create a loot box with items
Entity lootBox = ecs.Entity("Chest").Add<Container>().With<ContainedBy>(() =>
{
    ecs.Entity().IsA<IronSword>();
    ecs.Entity().IsA<WoodenArmor>();
    ecs.Entity().Add<Coin>().Set(new Amount { Value = 30 });
});

// Create a player entity with an inventory
Entity inventory = ecs.Entity().Add<Container>().With<ContainedBy>(() =>
{
    ecs.Entity().Add<Coin>().Set(new Amount { Value = 20 });
});

Entity player = ecs.Entity("Player")
    .Set(new Health { Value = 10})
    .Add<Inventory>(inventory);

// Print items in loot box
PrintItems(lootBox);

// Print items in player inventory
PrintItems(player);

// Copy items from loot box to player inventory
TransferItems(player, lootBox);

// Print items in player inventory after transfer
PrintItems(player);

// Print items in loot box after transfer
PrintItems(lootBox);

// Find armor entity & equip it
Entity armor = FindItemWithKind(player, ecs.Entity<Armor>());

if (armor != 0)
    armor.Add<Active>();

// Create a weapon to attack the player with
Entity mySword = ecs.Entity()
    .IsA<IronSword>();

// Attack player
Attack(player, mySword);

// Inventory tags/relationships
public struct Item { }        // Base item type
public struct Container { }   // Container tag
public struct Inventory { }   // Inventory tag
public struct ContainedBy { } // ContainedBy relationship

// Item / unit properties
public struct Active { }      // Item is active/worn

public struct Amount
{
    public int Value { get; set; } // Number of items the instance represents
}

public struct Health
{
    public int Value { get; set; } // Health of the item
}

public struct Attack
{
    public int Value { get; set; } // Amount of damage an item deals per use
}

// Items
public struct Sword { }
public struct Armor { }
public struct Coin { }

// Item prefab types
public struct WoodenSword { }
public struct IronSword { }
public struct WoodenArmor { }
public struct IronArmor { }

#endif

// Output:
// -- Chest's inventory:
//  - 1 IronSword (Sword)
//  - 1 WoodenArmor (Armor)
//  - 30 Coins (Coin)

// -- Player's inventory:
//  - 20 Coins (Coin)

// >> Transfer items from Chest to Player

// -- Player's inventory:
//  - 50 Coins (Coin)
//  - 1 IronSword (Sword)
//  - 1 WoodenArmor (Armor)

// -- Chest's inventory:
//  - << empty >>

// >> Player is attacked with a IronSword!
//  - Player defends with WoodenArmor
//  - WoodenArmor has 8 health left after taking 2 damage
//  - IronSword has 9 health left
