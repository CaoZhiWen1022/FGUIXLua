/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UI_HomePanel : GComponent
    {
        public GButton m_popupTest;
        public GButton m_Ripple;
        public GTextField m_text1;
        public GTextField m_text2;
        public GLoader m_DeerRoot;
        public const string URL = "ui://p20zd5lye0870";

        public static UI_HomePanel CreateInstance()
        {
            return (UI_HomePanel)UIPackage.CreateObject("Home", "HomePanel");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_popupTest = (GButton)GetChildAt(1);
            m_Ripple = (GButton)GetChildAt(2);
            m_text1 = (GTextField)GetChildAt(3);
            m_text2 = (GTextField)GetChildAt(4);
            m_DeerRoot = (GLoader)GetChildAt(8);
        }
    }
}