# MonoDetour

| MonoDetour | MonoDetour.HookGen | Community Support |
|:-:|:-:|:-:|
| [![MonoDetour](https://img.shields.io/nuget/v/MonoDetour?style=for-the-badge&logo=nuget)](https://www.nuget.org/packages/MonoDetour) | [![MonoDetour.HookGen](https://img.shields.io/nuget/v/MonoDetour.HookGen?style=for-the-badge&logo=nuget)](https://www.nuget.org/packages/MonoDetour.HookGen) | [![Discord](https://img.shields.io/discord/1377047282381361152?style=for-the-badge&label=Discord)](<https://discord.gg/Pt2BsA2cP4>) |

A `MonoMod.RuntimeDetour` wrapper optimized for convenience, based around HookGen with C# source generators.

> [!WARNING]
> This library is not fully released, and things *will* change.
>
> - Bugs and missing functionality is expected
> - Documentation may not reflect reality
>
> Major things missing for you may be:
>
> - Currently there is no HarmonyX interoperability
>   - Hooks that change control flow (HarmonyX prefix return false) will ignore MonoDetour/HarmonyX

## Add to Your Project

Change the version number to optimally the newest:

```xml
<ItemGroup>
  <PackageReference Include="MonoDetour.HookGen" Version="0.4.2" PrivateAssets="all" />
  <PackageReference Include="MonoDetour" Version="0.4.0" />
</ItemGroup>
```

MonoDetour.HookGen will automatically bring in the oldest MonoDetour reference it supports, so it's a good idea to specify the version for both.

Additionally MonoDetour automatically brings in the oldest MonoMod.RuntimeDetour version it supports, so also specify its version to the one you want (or don't if it's included by e.g. BepInEx references). MonoDetour should support MonoMod.RuntimeDetour versions 21.12.13.1 and 25.*, with possibly anything in between.

Note that MonoDetour.HookGen will strip unused generated hooks when building with `Release` configuration by default when `MonoDetourHookGenStripUnusedHooks` is undefined (evaluates to empty string).

You can configure this setting yourself however you wish, such as replicating the default behavior:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <MonoDetourHookGenStripUnusedHooks>true</MonoDetourHookGenStripUnusedHooks>
</PropertyGroup>
```

Having this setting enabled will be more expensive generation wise and intellisense may not keep up when writing hooks, e.g. Prefix, Postfix and ILHook methods may not immediately appear when typing `On.Namespace.Type.Method.` even if they have just been generated.

If the default HookGen namespace `On` causes collisions or you just don't like it, you can set it with the following property:

```xml
<PropertyGroup>
  <MonoDetourHookGenNamespace>On</MonoDetourHookGenNamespace>
</PropertyGroup>
```

## Documentation

- Website: <https://monodetour.github.io/>
- Join the Discord server for further support: <https://discord.gg/Pt2BsA2cP4>

## Usage

MonoDetour is mainly designed to be used with HookGen. That is, MonoDetour generates helpers hooks to make hooking easy.

You can use the generated hooks like so:

```cs
[MonoDetourTargets(typeof(SomeType))]
class SomeTypeHooks
{
    [MonoDetourHookInit]
    internal static void InitHooks()
    {
        // Note: this is using the default generated MonoDetourManager
        // MonoDetour.HookGen.DefaultMonoDetourManager.Instance by default.
        // Use it for managing your hooks.
        On.SomeNamespace.SomeType.SomeMethod.Prefix(Prefix_SomeType_SomeMethod);
    }

    static void Prefix_SomeType_SomeMethod(SomeType self)
    {
        Console.WriteLine("Hello from Prefix hook 1!");
    }
}
```

MonoDetour entirely relies on `ILHook`s for hooking similar to HarmonyX. But instead of having monolithic `ILHook` methods like in HarmonyX, MonoDetour maps every hook to a unique `ILHook`.

## Core Concepts

### MonoDetourManager

<https://monodetour.github.io/getting-started/monodetourmanager/>

## Why?

<https://monodetour.github.io/getting-started/why-monodetour/>

## How Does the HookGen Work?

<https://monodetour.github.io/getting-started/hookgen/>

## Credits

The HookGen source generator is *heavily* based on [MonoMod.HookGen.V2](<https://github.com/MonoMod/MonoMod/tree/hookgenv2>).
Without it, this project probably wouldn't exist.
