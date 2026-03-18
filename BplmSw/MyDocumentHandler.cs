using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Annotations;
using Xarial.XCad.Data;
using Xarial.XCad.Documents.Delegates;
using Xarial.XCad.Documents.Services;
using Xarial.XCad.Documents;
using Xarial.XCad;
using static BplmSw.Constants;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Documents.Structures;
using System.Windows.Controls;
using System.Security.Cryptography;
using System.Windows;

namespace BplmSw
{
    public class MyDocumentHandler : IDocumentHandler
    {
        private IXDocument m_Model;
        private List<IXProperty> m_Properties = new List<IXProperty>();
        private IXApplication m_app;
        private Dictionary<string, DateTime> m_Uid2CheckOutTime = new Dictionary<string, DateTime>(); // 保存零部件的签出时间点

        public void Init(IXApplication app, IXDocument model)
        {
            m_Model = model;
            m_app = app;

            m_Model.Closing += OnModelClosing;
            m_Model.Rebuilt += OnModelRebuilt;
            //m_Model.Selections.NewSelection += OnNewSelection;

            string objectType_CN = m_Model.Properties.GetOrPreCreate(ITEM_TYPE_CHINESE).Value?.ToString();
            if (string.IsNullOrEmpty(objectType_CN))
                return;

            foreach(var i in ConfigParser.SwAttrs[ConfigParser.SwItemTypeMap.CN2Ens[objectType_CN]])
            {
                var xProperty = m_Model.Properties.GetOrPreCreate(i.Description);
                xProperty.ValueChanged += OnPropertyValueChanged;
                m_Properties.Add(xProperty);
            }
        }

        private async void OnModelClosing(IXDocument doc, DocumentCloseType_e type)
        {
            //handle closing
            try
            {
                string uid = doc.Properties.GetOrPreCreate(PUID).Value?.ToString();
                string objectType_CN = doc.Properties.GetOrPreCreate(ITEM_TYPE_CHINESE).Value?.ToString();
                if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrEmpty(objectType_CN))
                    return;

                string objectTypeRev = ConfigParser.SwItemTypeMap.CN2Ens[objectType_CN] + REVISION_SUFFIX;
                var res = await HttpRequest.checkIn(uid, objectTypeRev);
                await BplmSwAddIn.Instance.UpdateCommandStateAsync(m_app.Documents.Active);
                if (!res.isSuccess)
                    return;

                m_Uid2CheckOutTime.Remove(uid);
            }
            catch (Exception ex)
            {
                m_app.ShowMessageBox(ex.Message);
            }
        }

        // 对同一个件，10分钟以内的多次修改只会触发一次签出，这是为了避免频繁修改频繁签出，这个有效时间会在关闭后刷新
        private async void OnModelRebuilt(IXDocument doc)
        {
            //handle rebuilt
            try
            {
                string uid = doc.Properties.GetOrPreCreate(PUID).Value?.ToString();
                DateTime currentTime = DateTime.Now;
                if (m_Uid2CheckOutTime.ContainsKey(uid))
                {
                    var lastCheckOutTime = m_Uid2CheckOutTime[uid];
                    TimeSpan timeDifference = currentTime - lastCheckOutTime;

                    // 判断距离上次签出是否小于10分钟
                    if (timeDifference.TotalMinutes < 10)
                        return;
                }

                string objectType_CN = doc.Properties.GetOrPreCreate(ITEM_TYPE_CHINESE).Value?.ToString();
                if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrEmpty(objectType_CN))
                    return;

                string objectTypeRev = ConfigParser.SwItemTypeMap.CN2Ens[objectType_CN] + REVISION_SUFFIX;
                var res = await HttpRequest.checkOut(uid, objectTypeRev);
                await BplmSwAddIn.Instance.UpdateCommandStateAsync(m_app.Documents.Active);
                if (!res.isSuccess)
                    throw new Exception(res.mesg);

                m_Uid2CheckOutTime[uid] = currentTime;
            }
            catch (Exception ex)
            {
                m_app.ShowMessageBox(ex.Message);
            }
        }

        private void OnNewSelection(IXDocument doc, IXSelObject selObject)
        {
            //handle new selection
        }

        private void OnPropertyValueChanged(IXProperty prp, object newValue)
        {
            //handle property change
            OnModelRebuilt(m_Model);
        }

        private void OnDimensionValueChanged(IXDimension dim, double newVal)
        {
            //handle dimension change
        }

        public void Dispose()
        {
            m_Model.Closing -= OnModelClosing;
            m_Model.Rebuilt -= OnModelRebuilt;
            //m_Model.Selections.NewSelection -= OnNewSelection;

            foreach(var i in m_Properties)
            {
                i.ValueChanged -= OnPropertyValueChanged;
            }
        }
    }
}