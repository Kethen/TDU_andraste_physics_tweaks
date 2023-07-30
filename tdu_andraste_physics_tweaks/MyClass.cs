using Andraste.Payload.ModManagement;
using Andraste.Payload.Native;
using System;
using System.IO;
using System.Text.Json;
using NLog;

namespace KatieCookie.tdu
{
	public class AndrastePhysicsTweaks : BasePlugin
	{
		public class Config
		{
			public float Gravity { get; set;}
			public float NormalModeMultiplier { get; set;}
			public String ForceHC { get; set;}
		}

		private String FormatConfig(Config config){
			return "Gravity: " + config.Gravity + ", NormalModeMultiplier: " + config.NormalModeMultiplier + ", ForceHC: " + config.ForceHC;
		}

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private bool _enabled;
		public override bool Enabled
		{
			get {
				return _enabled;
			}
			set
			{
				Config config = new Config ();
				config.Gravity = -9.81f;
				config.NormalModeMultiplier = 1.0f;
				config.ForceHC = "no";
				try{
					var config_json_path = ModInstance.ModSetting.ModPath + "\\config.json";
					Logger.Trace("Loading config from " + config_json_path);
					var config_json = File.ReadAllText(config_json_path);
					config = JsonSerializer.Deserialize<Config>(config_json);
				}catch(Exception e){
					Logger.Error("Cannot parse config.json, " + e);
					throw e;
				}

				IntPtr normal_mode_location = new IntPtr (0x009e2710);
				// mov ecx, dword ptr [00f8a21c]
				byte[] normal_mode_expectation = new byte[]{0x8b, 0x0d, 0x1c, 0xa2, 0xf8, 0x00};
				// mov ecx, <gravity value>
				byte[] normal_mode_gravity_bytes = BitConverter.GetBytes(config.NormalModeMultiplier);
				byte[] normal_mode_patch_target = new byte[] {0xc7, 0xc1, normal_mode_gravity_bytes[0], normal_mode_gravity_bytes[1], normal_mode_gravity_bytes[2], normal_mode_gravity_bytes[3]};
				InstructionPatcher normal_mode_patcher = new InstructionPatcher(normal_mode_location, normal_mode_expectation, normal_mode_patch_target);

				IntPtr gravity_location = new IntPtr (0x00f41cc4);
				// -9.81
				byte[] gravity_expectation = new byte[]{0xc3, 0xf5, 0x1c, 0xc1};
				byte[] gravity_patch_target = BitConverter.GetBytes (config.Gravity);
				InstructionPatcher gravity_patcher = new InstructionPatcher(gravity_location, gravity_expectation, gravity_patch_target);

				IntPtr force_hc_location = new IntPtr (0x008348a1);
				// mov al, [010e777c] ...
				byte[] force_hc_expectation = new byte[] {
					0xA0,
					0x7C,
					0x77,
					0x0E,
					0x01,
					0x84,
					0xC0,
					0x74,
					0x15,
					0x8A,
					0x45,
					0x18,
					0x84,
					0xC0,
					0x75,
					0x0E,
					0x8A,
					0x45,
					0x1C,
					0x84,
					0xC0,
					0x75,
					0x07
				};
				// nop; nop; ...; mov al, [ebp + param_2], test al, al; jz ...
				byte[] force_hc_player_racer = new byte[] {
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x8A,
					0x45,
					0x0C,
					0x84,
					0xC0,
					0x74,
					0x07
				};
				// nop instead of jz after the hc global variable check
				byte[] force_hc_player_only = new byte[] {
					0xA0,
					0x7C,
					0x77,
					0x0E,
					0x01,
					0x84,
					0xC0,
					0x90,
					0x90,
					0x8A,
					0x45,
					0x18,
					0x84,
					0xC0,
					0x75,
					0x0E,
					0x8A,
					0x45,
					0x1C,
					0x84,
					0xC0,
					0x75,
					0x07
				};
				// nop all, don't check, apply hc to everything
				byte[] force_hc_all = new byte[] {
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90,
					0x90
				};

				byte[] selected_patch = force_hc_player_only;
				bool force_hc = false;
				switch (config.ForceHC) {
				case "player_only":
					force_hc = true;
					break;
				case "player_and_ai_racer":
					force_hc = true;
					selected_patch = force_hc_player_racer;
					break;
				case "all":
					force_hc = true;
					selected_patch = force_hc_all;
					break;
				default:
					break;
				}

				InstructionPatcher force_hc_patcher = new InstructionPatcher (force_hc_location, force_hc_expectation, selected_patch);

				_enabled = value;

				normal_mode_patcher.Patch (value);
				gravity_patcher.Patch (value);
				force_hc_patcher.Patch (value && force_hc);

				if (value){
					Logger.Info("Enabling physics tweaks with " + FormatConfig(config));
				}else{
					Logger.Info("Disabling physics tweaks");
				}
			}
		}

		protected override void PluginLoad()
		{
		}

		protected override void PluginUnload()
		{
		}
	}
}