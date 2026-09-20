using System.Runtime.CompilerServices;
using System.Security.Permissions;
using BepInEx;
using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;
using static Player;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace RivuletPoleFix;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class RivuletPoleFixMain : BaseUnityPlugin {
    public const string PLUGIN_GUID = "zohnannor.rivuletpolefix";
    public const string PLUGIN_NAME = "Rivulet Pole Fix";
    public const string PLUGIN_VERSION = "1.0.0";

    private bool initDone = false;
    public static RivuletPoleFixOptions Options;

    private static readonly ConditionalWeakTable<Player, object> Sticky = new();
    private static readonly object StickyMarker = new();

    public void OnEnable() {
        On.RainWorld.OnModsInit += OnModsInit;
    }

    public void OnDisable() {
        On.RainWorld.OnModsInit -= OnModsInit;
        On.Player.Jump -= Player_Jump;
        On.Player.GrabVerticalPole -= Player_GrabVerticalPole;
    }

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self) {
        orig(self);
        if (initDone) {
            return;
        }

        Options = new RivuletPoleFixOptions();
        MachineConnector.SetRegisteredOI(PLUGIN_GUID, Options);

        On.Player.Jump += Player_Jump;
        On.Player.GrabVerticalPole += Player_GrabVerticalPole;

        Logger.LogDebug($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        initDone = true;
    }

    public void Player_Jump(
        On.Player.orig_Jump orig,
        Player self
    ) {
        if (
            self.animation == AnimationIndex.ClimbOnBeam
                && self.input[0].x != 0
                && self.isRivulet
                && Options.Enabled.Value
        ) {
            Sticky.Remove(self);
            Sticky.Add(self, StickyMarker);
        }
        orig(self);
    }

    public void Player_GrabVerticalPole(
        On.Player.orig_GrabVerticalPole orig,
        Player self
    ) {
        orig(self);
        // don't do anything if we're not rivulet, if we're already climbing,
        // standing on the ground, haven't been on the pole or if the mod is
        // disabled
        if (
            !self.isRivulet
                || self.animation == AnimationIndex.ClimbOnBeam
                || self.bodyChunks[1].ContactPoint.y < 0
                || !Options.Enabled.Value
                || !Sticky.TryGetValue(self, out _)
        ) {
            return;
        }
        Sticky.Remove(self);

        // the same thing as `orig` but we force it to work from farther away
        // when in the air
        IntVector2 tilePosition = self.room.GetTilePosition(self.mainBodyChunk.pos);
        // direction == 0 already checked by `orig`
        foreach (int direction in new[] { -1, 1 }) {
            if (!self.room.GetTile(tilePosition + new IntVector2(direction, 0)).verticalBeam) {
                continue;
            }
            IntVector2 pos = tilePosition + new IntVector2(direction, 0);
            self.room.PlaySound(SoundID.Slugcat_Grab_Beam, self.mainBodyChunk, loop: false, 1f, 1f);
            self.animation = AnimationIndex.ClimbOnBeam;
            self.flipDirection = self.bodyChunks[0].pos.x < self.room.MiddleOfTile(pos).x ? -1 : 1;
            self.bodyChunks[0].vel = new Vector2(0f, 0f);
            self.bodyChunks[0].pos.x = self.room.MiddleOfTile(pos).x;
            // don't try to grab another pole if we've already succeeded
            break;
        }
    }

}

public class RivuletPoleFixOptions : OptionInterface {
    public readonly Configurable<bool> Enabled;

    private OpTab mainTab;
    private OpCheckBox _enabledCheckbox;

    private const string desc = "Toggle the mod's functionality without restarting the game.";

    public RivuletPoleFixOptions() {
        Enabled = config.Bind("enabled", true);
    }

    public override void Initialize() {
        base.Initialize();

        mainTab = new OpTab(this, "Main");
        Tabs = [mainTab];

        _enabledCheckbox = new OpCheckBox(Enabled, 5f, 527f) {
            description = desc
        };

        mainTab.AddItems([
            _enabledCheckbox,
            new OpLabel(37f, 530f, "Enabled") {
                alignment = FLabelAlignment.Left,
                description = desc
            },
        ]);
    }
}
