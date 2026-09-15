/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace PopupTest
{
    public partial class UI_PopupTest : GComponent
    {
        public GButton m_closeBtn;
        public const string URL = "ui://pac0obpbn7im0";

        public static UI_PopupTest CreateInstance()
        {
            return (UI_PopupTest)UIPackage.CreateObject("PopupTest", "PopupTest");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_closeBtn = (GButton)GetChildAt(2);
        }
    }
}