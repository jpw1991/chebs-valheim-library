# Assembly Publicization

I find the best way to publicize Valheim assemblies is to do it directly on referencing it from the Valheim directory. This means it'll dynamically publicize whatever is in the Valheim install folder and it's overall less to manage.

To do this, I recommend the [BepInEx.AssemblyPublicizer.MSBuild](https://github.com/BepInEx/BepInEx.AssemblyPublicizer) nuget package. Then when you preference the DLL, use `Publicize="true"`. Here's a full example:

```xml
    <Reference Include="assembly_valheim" Publicize="true">
      <HintPath>..\..\..\.local\share\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll</HintPath>
    </Reference>
```

## Full Example

Here's a full example of the `CWJesse.BetterFPS.csproj.user` file with this working to dynamically reference and publicize the Valheim assembly. I chose this file as an example because it's smaller than in my projects.

<details><summary>CWJesse.BetterFPS.csproj.user</summary>
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <Version>0.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="0Harmony">
      <HintPath>..\..\..\.local\share\Steam\steamapps\common\Valheim\BepInEx\core\0Harmony.dll</HintPath>
    </Reference>
    <Reference Include="assembly_valheim" Publicize="true">
      <HintPath>..\..\..\.local\share\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll</HintPath>
    </Reference>
    <Reference Include="BepInEx">
      <HintPath>..\..\..\.local\share\Steam\steamapps\common\Valheim\BepInEx\core\BepInEx.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>..\..\..\.local\share\Steam\steamapps\common\Valheim\valheim_Data\Managed\UnityEngine.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.AnimationModule">
      <HintPath>..\..\..\.local\share\Steam\steamapps\common\Valheim\valheim_Data\Managed\UnityEngine.AnimationModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>..\..\..\.local\share\Steam\steamapps\common\Valheim\valheim_Data\Managed\UnityEngine.CoreModule.dll</HintPath>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="REM copy /Y /V &quot;CWJesse.BetterFPS.dll&quot; &quot;C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\CWJesse.BetterFPS.dll&quot;" />
  </Target>
</Project>
</details>