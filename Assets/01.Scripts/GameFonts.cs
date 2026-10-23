using UnityEngine;

namespace DuelHero.UI
{
    public static class GameFonts
    {
        private static Font ui;

        public static Font UI
        {
            get
            {
                if (ui == null) ui = Resources.Load<Font>("Fonts/BusanDandiM");
                return ui;
            }
        }
    }
}
