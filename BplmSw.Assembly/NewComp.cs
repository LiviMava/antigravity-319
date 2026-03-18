using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BplmSw;
using BplmSw.NewDoc;
using System.Collections.ObjectModel;
using SolidWorks.Interop.swconst;

namespace BplmSw.Assembly
{
    public class NewComp
    {
        private ISldWorks Sw;
        private IFrame iFrame;
        private ModelDoc2 activedDoc;
        public NewComp(ISldWorks Sw)
        {
            this.Sw = Sw;
            iFrame = Sw.Frame() as IFrame;
        }

        public void execute()
        {
            try
            {
                activedDoc = (ModelDoc2)Sw.ActiveDoc;
                if (2 != activedDoc.GetType())
                    throw new Exception("非装配体不允许新建组件");

                var window = new NewDoc.NewDocWindow(Sw, NewDocWindowMode.NoDrawingTemplate);
                window.DataPassed += NewDocWindow_DataPassed;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                iFrame.SetStatusBarText(ex.Message);
            }
        }

        private void NewDocWindow_DataPassed(object sender, ModelDoc2 doc)
        {
            try
            {
                AssemblyDoc assemDoc = (AssemblyDoc)activedDoc;
                string path = doc.GetPathName();
                Component2 insertedComp = assemDoc.AddComponent5(path, (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                    "", false, "", 0, 0, 0);
                Sw.CloseDoc(doc.GetTitle());

                // 添加固定约束
                string assemName = activedDoc.GetTitle().Substring(0, activedDoc.GetTitle().LastIndexOf("."));
                activedDoc.ClearSelection2(true);
                activedDoc.Extension.SelectByID2(insertedComp.Name2 + "@" + assemName, "COMPONENT", 0, 0, 0, false, 0, null, 0);
                assemDoc.FixComponent();
            }
            catch (Exception ex)
            {
                iFrame.SetStatusBarText(ex.Message);
            }
        }
    }
}