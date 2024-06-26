﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace HugsLib.Settings;

internal static class OptionsDialogExtensions {
	private static FieldInfo cachedModsField;
	private static FieldInfo hasModSettingsField;

	public static void InjectHugsLibModEntries(Dialog_Options dialog) {
		var ownedByHugsLibMods = HugsLibController.Instance.InitializedMods
			.Where(mod => mod.SettingsPackInternalAccess != null)
			.Select(mod => {
				var settingsPack = mod.SettingsPackInternalAccess;
				var contentPack = mod.ModContentPack ?? HugsLibController.OwnContentPack;
				return (settingsPack, contentPack);
			})
			.Append((settingsPack: HugsLibController.OwnSettingsPack, contentPack: HugsLibController.OwnContentPack))
			.ToArray();
		
		var ownedByUnknownMods = HugsLibController.SettingsManager.ModSettingsPacks
			.Except(ownedByHugsLibMods.Select(packs => packs.settingsPack))
			.Select(pack => (settingsPack: pack, contentPack: HugsLibController.OwnContentPack));
		
		var hugsLibEntries = ownedByHugsLibMods
			.Concat(ownedByUnknownMods)
			.Where(packs => packs.settingsPack.Handles.Any(h => !h.NeverVisible))
			.Select(packs => {
				var label = packs.settingsPack.EntryName.NullOrEmpty()
					? "HugsLib_setting_unnamed_mod".Translate().ToString()
					: packs.settingsPack.EntryName;
				return new SettingsProxyMod(label, packs.settingsPack, packs.contentPack);
			});

		var stockEntries = (IEnumerable<Mod>)cachedModsField.GetValue(dialog);
		var combinedEntries = stockEntries
			.Concat(hugsLibEntries)
			.OrderBy(m => m.SettingsCategory())
			.ToArray();

		cachedModsField.SetValue(dialog, combinedEntries);
		hasModSettingsField.SetValue(dialog, true);
	}

	public static Window GetModSettingsWindow(Mod forMod) {
		return forMod is SettingsProxyMod proxy
			? new Dialog_ModSettings(proxy.SettingsPack)
			: new RimWorld.Dialog_ModSettings(forMod);
	}

	public static void PrepareReflection() {
		const string cachedModsFieldName = "cachedModsWithSettings";
		cachedModsField =
			typeof(Dialog_Options).GetField(cachedModsFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
		if (cachedModsField == null || cachedModsField.FieldType != typeof(IEnumerable<Mod>))
			HugsLibController.Logger.Error($"Failed to reflect {nameof(Dialog_Options)}.{cachedModsFieldName}");
		const string hasModSettingsFieldName = "hasModSettings";
		hasModSettingsField =
			typeof(Dialog_Options).GetField(hasModSettingsFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
		if (hasModSettingsField == null || hasModSettingsField.FieldType != typeof(bool))
			HugsLibController.Logger.Error($"Failed to reflect {nameof(Dialog_Options)}.{hasModSettingsFieldName}");
	}
}

internal class SettingsProxyMod : Mod {
	public ModSettingsPack SettingsPack { get; }
	private readonly string entryLabel;

	[UsedImplicitly]
	public SettingsProxyMod(ModContentPack content) : base(content) {
	}

	public SettingsProxyMod(
		string entryLabel, ModSettingsPack settingsPack, ModContentPack contentPack) : base(contentPack) {
		SettingsPack = settingsPack;
		this.entryLabel = entryLabel;
	}

	public override string SettingsCategory() {
		return entryLabel;
	}
}