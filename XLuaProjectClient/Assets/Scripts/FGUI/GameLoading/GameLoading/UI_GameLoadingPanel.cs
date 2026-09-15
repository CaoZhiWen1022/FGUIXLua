/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace GameLoading
{
    public partial class UI_GameLoadingPanel : GComponent
    {
        public GProgressBar m_bar;
        public GTextField m_test1;
        public GTextField m_test2;
        public GTextField m_test3;
        public const string URL = "ui://rcpqb2f6wuau0";

        public static UI_GameLoadingPanel CreateInstance()
        {
            return (UI_GameLoadingPanel)UIPackage.CreateObject("GameLoading", "GameLoadingPanel");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_bar = (GProgressBar)GetChildAt(2);
            m_test1 = (GTextField)GetChildAt(3);
            m_test2 = (GTextField)GetChildAt(4);
            m_test3 = (GTextField)GetChildAt(5);
        }
    }
}