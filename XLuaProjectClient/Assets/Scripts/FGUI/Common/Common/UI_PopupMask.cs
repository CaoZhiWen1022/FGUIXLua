/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Common
{
    public partial class UI_PopupMask : GComponent
    {
        public GGraph m_mask;
        public const string URL = "ui://rovr2x2qn7im3";

        public static UI_PopupMask CreateInstance()
        {
            return (UI_PopupMask)UIPackage.CreateObject("Common", "PopupMask");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_mask = (GGraph)GetChildAt(0);
        }
    }
}