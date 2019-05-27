using UnicontaClient.Models;
using UnicontaClient.Utilities;
using Uniconta.ClientTools;
using Uniconta.ClientTools.Controls;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.ClientTools.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Uniconta.Common;
using Uniconta.DataModel;
using System.Threading;
using System.Windows.Data;
using System.Collections;
using Uniconta.API.System;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public class TabledataGrid : CorasauDataGridClient
    {
        public override Type TableType { get { return UserTableType; } }
        public override bool Readonly { get { return !IsEditable; } }
        public bool IsEditable;
        public Type UserTableType;
    }
    public partial class UserTableData : GridBasePage
    {
        public override string LayoutName { get { return string.Format("TableData_{0}", layoutname != null ? layoutname : this.thMaster?._Name); } }

        public override string NameOfControl { get { return TabControls.UserTableData; } }

        TableHeader thMaster;
        bool isTableDataWithKey;
        string layoutname;
        UnicontaBaseEntity master;
        string mastertabName;
        public UserTableData(TableHeader thMaster, string layoutname, UnicontaBaseEntity masterRecord)
            : base(thMaster)
        {
            this.thMaster = thMaster;
            Layout._SubId = api.CompanyId;
            if (layoutname.Contains(';'))
            {
                var param = layoutname.Split(';');
                mastertabName = param[1];
                layoutname = param[0];
            }
            this.layoutname = layoutname;
            Initialize(thMaster, masterRecord);
        }


        public UserTableData(TableHeader thMaster, string layoutname)
            : this(thMaster, layoutname, null as UnicontaBaseEntity)
        {
        }

        public UserTableData(TableHeader master) : base(master)
        {
            this.thMaster = master;
            Layout._SubId = api.CompanyId;
            Initialize(master, null);
        }
        public UserTableData(TableHeader thMaster, string layoutname, SynchronizeEntity syncEntity)
         : base(syncEntity, true)
        {
            this.thMaster = thMaster;
            Layout._SubId = api.CompanyId;
            if (layoutname.Contains(';'))
            {
                var param = layoutname.Split(';');
                mastertabName = param[1];
                layoutname = param[0];
            }
            this.layoutname = layoutname;
            Initialize(thMaster, syncEntity?.Row);
        }
        protected override void SyncEntityMasterRowChanged(UnicontaBaseEntity args)
        {
            dgTabledataGrid.UpdateMaster(args);
            SetHeader(args);
            InitQuery();
        }
        void SetHeader(UnicontaBaseEntity args)
        {
            var syncMaster = args;
            string header = null;
            if (syncMaster != null)
            {
                string key = (args as TableData)?._KeyName;
                if (string.IsNullOrEmpty(key))
                    key = (args as IdKey)?.KeyName;
                if (!string.IsNullOrEmpty(key))
                    header = string.Format("{0}:{1}/{2}", Uniconta.ClientTools.Localization.lookup("Data"), mastertabName, key);
            }
            if (header != null)
                SetHeader(header);
        }
        private void Initialize(TableHeader thMaster, UnicontaBaseEntity masterRecord)
        {
            InitializeComponent();
            master = masterRecord;
            dgTabledataGrid.UserTableType = thMaster.UserType;
            dgTabledataGrid.IsEditable = thMaster._EditLines;

            // first call setUserFields after grid is setup correctly
            setUserFields(thMaster);
            RemoveMenuItem();
            LayoutControl = detailControl.layoutItems;
            localMenu.TableName = thMaster?._Name;
            SetRibbonControl(localMenu, dgTabledataGrid);
            localMenu.TableName = thMaster?._Name;
            dgTabledataGrid.api = api;
            dgTabledataGrid.BusyIndicator = busyIndicator;

            localMenu.OnItemClicked += localMenu_OnItemClicked;
            dgTabledataGrid.UpdateMaster(masterRecord);
        }

        List<TableHeader> dtlTables;
        void RemoveMenuItem()
        {
            RibbonBase rb = (RibbonBase)localMenu.DataContext;
            if (!this.thMaster._Attachment)
                UtilDisplay.RemoveMenuCommand(rb, new string[] { "AddDoc", "AddNote" });
            if (dgTabledataGrid.IsEditable)
                UtilDisplay.RemoveMenuCommand(rb, new string[] { "AddItem", "EditItem" });
            else
                UtilDisplay.RemoveMenuCommand(rb, new string[] { "AddRow", "CopyRow", "DeleteRow", "SaveGrid" });
            dtlTables = Utilities.Utility.GetDefaultCompany().UserTables.Where(x => x._MasterTable == thMaster._Name).ToList();
            if (dtlTables.Count > 0)
            {
                var childList = new List<TreeRibbon>();
                var childRibbon = new TreeRibbon();
                string nodeText = string.Empty;
                string tblName = string.Empty;
                if (dtlTables.Count > 1)
                    nodeText = Uniconta.ClientTools.Localization.lookup("UserTableData");
                else
                {
                    var tbl = dtlTables.FirstOrDefault();
                    if (tbl != null)
                    {
                        nodeText = !string.IsNullOrEmpty(tbl._Prompt) ? UserFieldControl.LocalizePrompt(tbl._Prompt) : tbl._Name;
                        tblName = tbl._Name;
                    }
                }
                childRibbon.Name = nodeText;
                childRibbon.ActionName = dtlTables.Count > 1 ? "" : string.Format("UserTableData;{0}", tblName);
                childRibbon.Child = childList;
                childRibbon.Glyph = ";component/Assets/img/UserFieldData_32x32.png";
                childRibbon.LargeGlyph = ";component/Assets/img/UserFieldData_32x32.png";
                var userRbnList = new List<TreeRibbon>();
                userRbnList.Add(childRibbon);
                var treeRibbon = new TreeRibbon();
                treeRibbon.Child = userRbnList;
                rb.rbnlist.Add(treeRibbon);
                if (dtlTables.Count > 1)
                {
                    var ribbonList = new List<TreeRibbon>();
                    foreach (var ur in dtlTables)
                    {
                        var ribbonNode = new TreeRibbon();
                        ribbonNode.Name = !string.IsNullOrEmpty(ur._Prompt) ? UserFieldControl.LocalizePrompt(ur._Prompt) : ur._Name;
                        ribbonNode.ActionName = string.Format("UserTableData;{0}", ur._Name);
                        ribbonNode.LargeGlyph = ";component/Assets/img/CopyUserTable_16x16.png";
                        ribbonNode.Glyph = ";component/Assets/img/CopyUserTable_16x16.png";
                        ribbonNode.Child = new List<TreeRibbon>();
                        ribbonList.Add(ribbonNode);
                    }
                    childList.AddRange(ribbonList);
                }
                rb.RefreshMenuItem(userRbnList);
            }
        }

        void setUserFields(TableHeader thMaster)
        {
            var userType = thMaster.UserType;
            if (userType == null)
            {
                UnicontaMessageBox.Show(Uniconta.ClientTools.Localization.lookup("UserTypeMasterError"), Uniconta.ClientTools.Localization.lookup("Error"));
                return;
            }
            var row = Activator.CreateInstance(userType) as TableData;
            var UserFieldDef = row.UserFieldDef();
            localMenu.UserFields = UserFieldDef;

            if (dgTabledataGrid.Columns.Count == 0)
            {

                if (thMaster._HasPrimaryKey)
                    UserFieldControl.CreateKeyFieldsOnGrid(dgTabledataGrid, thMaster._PKprompt);
                if (UserFieldDef != null)
                    UserFieldControl.CreateUserFieldOnGrid(dgTabledataGrid, UserFieldDef, (RowIndexConverter)this.Resources["RowIndexConverter"], api, !dgTabledataGrid.IsEditable, useBinding: false);
                Layout._SubId = api.CompanyId;
            }
            else
                SetColBinding(UserFieldDef);
            detailControl.CreateUserField(UserFieldDef, thMaster._HasPrimaryKey, this.api, thMaster._PKprompt);
            if (thMaster._MasterTable != null)
            {

                var masterColumn = new CorasauDataGridTemplateColumnClient();
                masterColumn.FieldName = "MasterKey";
                masterColumn.RefType = row.MasterType;
                if (masterColumn.RefType == null)
                {
                    masterColumn.RefType = typeof(Uniconta.DataModel.TableDataWithKey);
                    masterColumn.TableId = row.MasterTableId;
                }
                if (dgTabledataGrid.IsEditable)
                    masterColumn.AllowEditing = DevExpress.Utils.DefaultBoolean.True;
                else
                    masterColumn.AllowEditing = DevExpress.Utils.DefaultBoolean.False;

                dgTabledataGrid.Columns.Add(masterColumn);

                dgTabledataGrid.LookupFieldsAdded = true;
            }
        }

        void SetColBinding(TableField[] UserFieldDef)
        {
            var CurrentCulture = Thread.CurrentThread.CurrentCulture;
            var Path = new PropertyPath("UserField");
            int i = 0;
            foreach (var def in UserFieldDef)
            {
                if (def._Delete || def._Hide)
                    continue;
                var b = new Binding();
                b.Converter = (RowIndexConverter)this.Resources["RowIndexConverter"];
                b.ConverterParameter = def;
                b.Path = Path;
                b.ConverterCulture = CurrentCulture;
                if (def._ReadOnly)
                    b.Mode = BindingMode.OneWay;
                dgTabledataGrid.Columns[i].Binding = b;
                i++;
            }
        }
        void localMenu_OnItemClicked(string ActionType)
        {
            var selectedItem = dgTabledataGrid.SelectedItem as TableData;
            if (ActionType.Contains("UserTableData"))
            {
                if (selectedItem == null)
                    return;
                var sender = ribbonControl.senderRibbonButton;
                var tabName = sender?.Content;
                var tableName = (sender.Tag as string)?.Split(';')[1];
                var userTable = dtlTables.Where(x => x._Name == tableName).FirstOrDefault();
                object[] parmtbldata = new object[3];
                parmtbldata[0] = userTable;
                parmtbldata[1] = string.Format("{0};{1}", tableName, tabName);
                parmtbldata[2] = dgTabledataGrid.syncEntity;
                AddDockItem(TabControls.UserTableData, parmtbldata, string.Format("{0}:{1}/{2}", Uniconta.ClientTools.Localization.lookup("Data"), tabName, selectedItem._KeyName));
                return;
            }
            switch (ActionType)
            {
                case "AddItem":
                    if (this.thMaster?.UserType != null)
                    {
                        object[] param = new object[3];
                        param[0] = api;
                        param[1] = this.thMaster;
                        param[2] = this.master;
                        AddDockItem(TabControls.UserTableDataPage2, param, (this.thMaster as TableHeader)?._Name, ";component/Assets/img/Add_16x16.png");
                    }
                    break;
                case "EditItem":
                    if (selectedItem != null)
                    {
                        object[] parameter = new object[2];
                        parameter[0] = selectedItem;
                        parameter[1] = this.thMaster;
                        AddDockItem(TabControls.UserTableDataPage2, parameter, (this.thMaster as TableHeader)?._Name, ";component/Assets/img/Edit_16x16.png");
                    }
                    break;
                case "AddNote":
                    if (selectedItem != null)
                        AddDockItem(TabControls.UserNotesPage, selectedItem, string.Format("{0}: {1}", Uniconta.ClientTools.Localization.lookup("UserNotesInfo"), this.thMaster._Name));
                    break;
                case "AddDoc":
                    if (selectedItem != null)
                        AddDockItem(TabControls.UserDocsPage, selectedItem, string.Format("{0}: {1}", Uniconta.ClientTools.Localization.lookup("Documents"), this.thMaster._Name));
                    break;
                case "RefreshGrid":
                    if (gridControl.Visibility == Visibility.Visible)
                        gridRibbon_BaseActions(ActionType);
                    break;
                case "AddRow":
                    dgTabledataGrid.AddRow();
                    break;
                case "CopyRow":
                    dgTabledataGrid.CopyRow();
                    break;
                case "SaveGrid":
                    dgTabledataGrid.SaveData();
                    break;
                case "DeleteRow":
                    dgTabledataGrid.DeleteRow();
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
        }

        public override void Utility_Refresh(string screenName, object argument = null)
        {
            if (screenName == TabControls.UserTableDataPage2)
            {
                dgTabledataGrid.UpdateItemSource(argument);
                localMenu_OnItemClicked("RefreshGrid");
                if (dgTabledataGrid.Visibility == Visibility.Collapsed)
                    detailControl.Refresh(argument, dgTabledataGrid.SelectedItem);
            }
        }
    }
}