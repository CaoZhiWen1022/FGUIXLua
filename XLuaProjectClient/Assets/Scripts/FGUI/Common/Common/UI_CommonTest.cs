/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Common
{
    public partial class UI_CommonTest : GComponent
    {
        public GTextField m_commonText;
        public const string URL = "ui://rovr2x2qe0871";

        public static UI_CommonTest CreateInstance()
        {
            return (UI_CommonTest)UIPackage.CreateObject("Common", "CommonTest");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_commonText = (GTextField)GetChildAt(1);
        }
    }
}