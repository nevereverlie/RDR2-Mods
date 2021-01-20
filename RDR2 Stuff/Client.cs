using RDR2;
using RDR2.Math;
using RDR2.Native;
using RDR2.UI;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RDR2_Stuff
{
    public class Client : Script
    {
        private Vector3 lastHitPosition = Vector3.Zero;
        private Vector3 lastTeleportPosition = Game.Player.Character.Position;

        private bool IsTeleportEnabled = false; 
        private bool IsExplosiveShotgunsEnabled = false;

        public Client()
        {
            KeyDown += BowTeleportEventHandler;
            KeyUp += UIEventHandler;
            Tick += TickEvent;
            Interval = 1;
        }

        private void UIEventHandler(object sender, KeyEventArgs e)
        {
            switch(e.KeyCode)
            {
                case Keys.F1:
                    IsTeleportEnabled = !IsTeleportEnabled;
                    if (IsTeleportEnabled)
                    {
                        RDR2.UI.Screen.ShowSubtitle("Teleport bow enabled");
                    }
                    else
                    {
                        RDR2.UI.Screen.ShowSubtitle("Teleport bow disabled");
                    }
                    break;

                case Keys.F2:
                    IsExplosiveShotgunsEnabled = !IsExplosiveShotgunsEnabled;
                    if (IsExplosiveShotgunsEnabled)
                    {
                        RDR2.UI.Screen.ShowSubtitle("Explosive shotguns enabled");
                    }
                    else
                    {
                        RDR2.UI.Screen.ShowSubtitle("Explosive shotguns disabled");
                    }
                    break;
            }
        }

        private void BowTeleportEventHandler(object sender, KeyEventArgs e)
        {
            if (IsTeleportEnabled)
            {
                BowTeleport(e);
            }
        }
        private void BowTeleport(KeyEventArgs e)
        {
            if (!CurrentWeapon("Bow"))
            {
                return;
            }
            if (e.KeyCode == Keys.T)
            {
                Vector3 aimPosition = GetAimPosition();

                if (lastTeleportPosition != aimPosition && aimPosition.DistanceTo(Game.Player.Character.Position) < 200f)
                {
                    Function.Call(Hash.START_PLAYER_TELEPORT, Game.Player, aimPosition.X, aimPosition.Y, aimPosition.Z, 0f); //Teleport to coords
                    lastTeleportPosition = aimPosition;
                }
            }
        }
        private void TrackAimPosition() // enable it for debug purposes
        {
            Vector3 aimPosition = GetAimPosition();
            var txt = new TextElement(aimPosition.ToString(), new PointF(300f, 300f), 0.3f);
            txt.Draw();
            Vector2 aimPosition2D = World3DToScreen2d(aimPosition);
            DrawRect(aimPosition2D.X, aimPosition2D.Y, 0.005f, 0.005f, Color.Red);
        }
        private static Vector3 GetAimPosition()
        {
            //get AimPosition
            Vector3 camPos = Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_COORD);
            Vector3 camRot = Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_ROT);

            float retz = camRot.Z * 0.0174532924F;
            float retx = camRot.X * 0.0174532924F;
            float absx = (float)Math.Abs(Math.Cos(retx));
            Vector3 camStuff = new Vector3((float)Math.Sin(retz) * absx * -1, (float)Math.Cos(retz) * absx, (float)Math.Sin(retx));

            //AimPosition result
            RaycastResult ray = World.Raycast(camPos, camPos + camStuff * 1000, IntersectOptions.Everything, Game.Player.Character);
            if (!ray.DitHit || ray.HitPosition == default(Vector3) || Vector3.Distance(camPos, ray.HitPosition) >= 500f)
            {
                return Vector3.Zero;
            }
            Vector3 aimPosition = ray.HitPosition;
            return aimPosition;
        }
        public static void DrawRect(float xPos, float yPos, float xScale, float yScale, Color color)
        {
            try
            {
                Function.Call(Hash.DRAW_RECT, xPos, yPos, xScale, yScale, color.R, color.G, color.B, color.A);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "log.txt"), ex.Message);
            }
        }
        Vector2 World3DToScreen2d(Vector3 pos)
        {
            var x2dp = new OutputArgument();
            var y2dp = new OutputArgument();
            Function.Call<bool>(Hash.GET_SCREEN_COORD_FROM_WORLD_COORD, pos.X, pos.Y, pos.Z, x2dp, y2dp);
            return new Vector2(x2dp.GetResult<float>(), y2dp.GetResult<float>());
        }




        private void TickEvent(object sender, EventArgs e)
        {
            if (Game.IsKeyPressed(Keys.F3))
            {
                string resultPed = GetUserInput();

                PedHash pedHash = Function.Call<PedHash>(Hash.GET_HASH_KEY, resultPed);

                Vector3 aimPos = GetAimPosition();

                CreatePed(pedHash, aimPos.X, aimPos.Y, aimPos.Z, aimPos.ToHeading());
            }

            if (IsExplosiveShotgunsEnabled)
            {
                ExplosiveShotguns();
            }
        }
        private string GetUserInput()
        {
            DisplayOnScreenKeyboard("PED SELECT", "Ped", 50);
            while (UpdateOnScreenKeyboard() != 1)
            {
                Wait(0);
            }
            string resultPed = Function.Call<string>(Hash.GET_ONSCREEN_KEYBOARD_RESULT);
            Function.Call(Hash._CANCEL_ONSCREEN_KEYBOARD);
            RDR2.UI.Screen.ShowSubtitle(resultPed);
            return resultPed;
        }
        private void DisplayOnScreenKeyboard(string caption, string defaultText, int maxInputLength)
        {
            Function.Call(Hash.DISPLAY_ONSCREEN_KEYBOARD, 0, caption, "", defaultText, "", "", "", maxInputLength);
        }
        private int CreatePed(PedHash ped, float posx, float posy, float posz, float heading)
        {
            var task = LoadPed(ped); 
            _ = task;
            int pedToCreate = Function.Call<int>(Hash.CREATE_PED, (uint)ped, posx, posy, posz, heading, true, true, true, true);
            //Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, pedToCreate, true, true); // setting this true for this ped not "despawn"
            SetRandomOutfitVariation(pedToCreate); 
            Function.Call(Hash.SET_MODEL_AS_NO_LONGER_NEEDED, ((uint)ped)); 
            return pedToCreate;
        }
        private void SetRandomOutfitVariation(int ped)
        {
            Function.Call((Hash)0x283978A15512B2FE, ped, true);
        }
        private bool LoadPed(PedHash pedHash)
        {
            if (LoadModel((int)pedHash))
                return true;
            else
                return false;
        }
        private bool LoadModel(int modelHash)
        {
            if (Function.Call<bool>(Hash.IS_MODEL_VALID, modelHash))
            {
                Function.Call(Hash.REQUEST_MODEL, modelHash);
                while (!Function.Call<bool>(Hash.HAS_MODEL_LOADED, modelHash))
                {
                    Wait(100);
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        private int UpdateOnScreenKeyboard()
        {
            return Function.Call<int>(Hash.UPDATE_ONSCREEN_KEYBOARD);
        }


        private void ExplosiveShotguns()
        {
            if (!CurrentWeapon("Shotgun"))
            {
                return;
            }

            var hitPosHash = new OutputArgument();
            Function.Call(Hash.GET_PED_LAST_WEAPON_IMPACT_COORD, Game.Player.Character.Handle, hitPosHash);
            var hitPos = hitPosHash.GetResult<Vector3>();
            if (lastHitPosition != hitPos)
            {
                Function.Call(Hash.ADD_EXPLOSION, hitPos.X, hitPos.Y, hitPos.Z, 1, 2.0, 1, 0, 1);
                lastHitPosition = hitPos;
            }
        }
        private bool CurrentWeapon(string weaponName)
        {
            return GetCurrentWeapon().ToString().Contains(weaponName);
        }
        private WeaponHash GetCurrentWeapon()
        {
            var outWeapon = new OutputArgument();
            Function.Call(Hash.GET_CURRENT_PED_WEAPON, Game.Player.Character.Handle, outWeapon);
            var currentWeapon = outWeapon.GetResult<WeaponHash>();
            return currentWeapon;
        }
    }
}
