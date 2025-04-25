using System;
using BepInEx;
using UnityEngine;
using System.Collections.Generic;
using MonoMod.RuntimeDetour;
using System.Reflection;
using RegionKit.Modules.RoomSlideShow;

namespace GearSounds {
    [BepInDependency("rwmodding.coreorg.rk", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("gearsounds", "Gear Sounds", "2.0")]
    class Plugin : BaseUnityPlugin {
        public Type playStateType;
        public Type frameType;
        public PropertyInfo currentStepProperty;
        public FieldInfo slideshowRectDataIdProperty;

        private HUD.HUD hud;
        private Room roomToPlaySoundsIn;
        private bool shouldPlaySounds;

        private HashSet<object> resetThings = new HashSet<object>();
        private object timeGetter;
        private bool hasTimeGetterBeenSet;

        private int soundResetFrame;
        
        public delegate void orig_Update(object self);
        public bool valid = false;

        private Hook updateHook;

        public static Plugin Instance { get; private set; }


        private SoundID GearSound;

        public static void Playstate_Update(orig_Update orig, object self) {
            var ticksInCurrentFrameProperty = self.GetType().GetProperty("TicksInCurrentFrame");
            var ticksSinceStartProperty = self.GetType().GetProperty("TicksSinceStart");

            for (int i = 0; i <= 0; i++) {
                if (Instance.roomToPlaySoundsIn == null) break;
                if (!Instance.shouldPlaySounds) break;
                if (ticksInCurrentFrameProperty == null) break;

                int ticksSinceStart = (int) ticksSinceStartProperty.GetValue(self);
                var currentStep = Instance.currentStepProperty.GetValue(self);

                if (currentStep == null) break;

                if (currentStep.GetType() != Instance.frameType) break;
                if (!Instance.hasTimeGetterBeenSet) { Instance.timeGetter = self; Instance.hasTimeGetterBeenSet = true; }

                Instance.resetThings.Add(self);

                if (Instance.timeGetter != self) continue;

                if (ticksSinceStart == 1) {
                    Instance.PlaySounds();
                    // SoundID soundID = Sounds.GearSounds[Instance.Random()];
                    // Instance.roomToPlaySoundsIn.PlaySound(soundID, 0.0f, 1.0f, 1.0f);
                    // // Instance.hud.PlaySound();
                    // Instance.soundResetFrame = 10;
                }
            }

            orig(self);
        }

        private void PlaySounds() {
            if (soundResetFrame > 0) return;
            if (roomToPlaySoundsIn?.roomSettings?.placedObjects == null) {
                Debug.LogError("roomToPlaySoundsIn or placedObjects is null");
                return;
            }

            hud.PlaySound(GearSound);
            
            // Logger.LogInfo("PLAYING SOUND YIPPEE!!");

            soundResetFrame = 10;
        }

        private bool IdGood(string id) {
            if (id == "smallgear" || id == "smallgearflip" || id == "biggear" || id == "biggearflip" ||
                id == "smallgear2" || id == "smallgearflip2" || id == "biggear2" || id == "biggearflip2" ||
                id == "smallgear3" || id == "smallgearflip3" || id == "biggear3" || id == "biggearflip3" ||
                id == "smallgear4" || id == "smallgearflip4" || id == "biggear4" || id == "biggearflip4") {
                return true;
            }

            return false;
        }

        private void SetPlayStateIndicies(object playState, object copy) {
            var currentIndexProperty = playState.GetType().GetProperty("CurrentIndex");
            var ticksSinceStartProperty = playState.GetType().GetProperty("TicksSinceStart");

            var ownerProperty = playState.GetType().GetField("owner");
            var owner = ownerProperty.GetValue(playState);
            var playbackStepsProperty = owner.GetType().GetField("playbackSteps");
            var playbackSteps = playbackStepsProperty.GetValue(owner);
            var countProperty = playbackSteps.GetType().GetProperty("Count");
            var idProperty = owner.GetType().GetField("id");

            if (!IdGood((string) idProperty.GetValue(owner))) return;

            int count = (int) countProperty.GetValue(playbackSteps);

            var setter1 = currentIndexProperty.GetSetMethod(nonPublic: true);
            var setter2 = ticksSinceStartProperty.GetSetMethod(nonPublic: true);
            if (setter1 != null && setter2 != null) {
                setter1.Invoke(playState, new object[] { ((int) currentIndexProperty.GetValue(copy)) % count });
                setter2.Invoke(playState, new object[] { ticksSinceStartProperty.GetValue(copy) });
            }
        }

        public void OnEnable() {
            Instance = this;
            GearSound = new SoundID("gearspin", true);
            
            // Logger.LogInfo(GearSound);
            // Logger.LogInfo("Sound Loaded!!!");

            playStateType = Type.GetType("RegionKit.Modules.RoomSlideShow.PlayState, RegionKit");
            frameType = Type.GetType("RegionKit.Modules.RoomSlideShow.Frame, RegionKit");
            currentStepProperty = playStateType.GetProperty("CurrentStep", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            slideshowRectDataIdProperty = typeof(SlideShowRectData).GetField("id", BindingFlags.Instance | BindingFlags.Public);

            valid = playStateType != null && frameType != null && currentStepProperty != null && slideshowRectDataIdProperty != null;

            if (!valid) {
                Logger.LogWarning("One or more values are null. Please ensure RegionKit is installed.");
                return;
            }

            // Logger.LogInfo("Found RegionKit");
            
            MethodInfo updateMethod = playStateType?.GetMethod("Update");

            if (updateMethod != null) {
                // Logger.LogInfo("Hooking...");
                updateHook = new Hook(updateMethod, typeof(Plugin).GetMethod("Playstate_Update", BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance));
            } else {
                Logger.LogError("Failed hooking to update method");
            }

            On.Player.Update += PlayerUpdateHook;
            On.RainWorldGame.Update += RainWorldGameUpdate;
            
            Logger.LogInfo("Gear Sounds Loaded");
        }

        void RainWorldGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self) {
            soundResetFrame--;

            orig(self);
            
            hud = self.cameras[0].hud;
            
            // if (Input.GetKeyDown(KeyCode.J)) {
                // Logger.LogInfo("Playing Sound");
                // hud.PlaySound(GearSound);
            // }

            if (!hasTimeGetterBeenSet) return;

            foreach (object thing in resetThings) {
                if (thing == timeGetter) continue;

                SetPlayStateIndicies(thing, timeGetter);
            }
        }

		void PlayerUpdateHook(On.Player.orig_Update orig, Player self, bool eu) {
			orig(self, eu);

            if (roomToPlaySoundsIn == self.room) return;
            if (self.room == null) return;

            shouldPlaySounds = false;
            hasTimeGetterBeenSet = false;
            timeGetter = null;
            roomToPlaySoundsIn = self.room;
            
            foreach (PlacedObject item in self.room.roomSettings.placedObjects) {
                if (item?.data?.GetType() != typeof(SlideShowRectData)) continue;

                string id = (string) slideshowRectDataIdProperty.GetValue(item.data);

                if (IdGood(id)) {
                    shouldPlaySounds = true;
                    break;
                }
            }
		}

        public void OnDisable() {
            updateHook?.Dispose();
            
            On.Player.Update -= PlayerUpdateHook;
            On.RainWorldGame.Update -= RainWorldGameUpdate;
        }
    }

}