using UnicontaClient.Models;
using DevExpress.Data.Filtering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
using Uniconta.ClientTools.Controls;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.ClientTools.Util;
using Uniconta.Common;
using Uniconta.DataModel;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public class VouchersGridLocal : VouchersGrid
    {
        public override Type TableType { get { return typeof(VouchersClientLocal); } }
        public override bool SingleBufferUpdate { get { return false; } }
    }
    public class GLDailyJournalLineGridLocal : GLDailyJournalLineGrid
    {
        public override bool SingleBufferUpdate { get { return false; } }
    }
    public partial class MatchPhysicalVoucherToGLDailyJournalLines : GridBasePage
    {
        GLDailyJournalClient masterRecord;
        ItemBase ibase;
        static bool AssignText;
        public MatchPhysicalVoucherToGLDailyJournalLines(UnicontaBaseEntity master)
            : base(null)
        {
            InitializeComponent();
            dgGldailyJournalLinesGrid.masterRecords = new List<UnicontaBaseEntity>() { master };
            dgGldailyJournalLinesGrid.api = api;
            dgvoucherGrid.masterRecords = new List<UnicontaBaseEntity>() { new Uniconta.DataModel.DocumentNoRef() };
            dgvoucherGrid.api = api;
            dgvoucherGrid.Readonly = true;

            masterRecord = master as GLDailyJournalClient;

            if (!api.CompanyEntity._UseVatOperation)
#if !SILVERLIGHT
                VatOperation.Visible = VatOffsetOperation.Visible = false;
#else
                VatOperation.Visibility = VatOffsetOperation.Visibility = Visibility.Collapsed;
#endif
            if (!masterRecord._TwoVatCodes)
#if !SILVERLIGHT
                OffsetVat.Visible = VatOffsetOperation.Visible = false;
#else
                OffsetVat.Visibility = VatOffsetOperation.Visibility = Visibility.Collapsed;
#endif
            SetRibbonControl(localMenu, dgGldailyJournalLinesGrid);
            localMenu.OnItemClicked += localMenu_OnItemClicked;
            dgvoucherGrid.RowDoubleClick += dgvoucherGrid_RowDoubleClick;
            dgGldailyJournalLinesGrid.SelectedItemChanged += DgGldailyJournalLinesGrid_SelectedItemChanged;
            dgGldailyJournalLinesGrid.View.DataControl.CurrentItemChanged += DataControl_CurrentItemChanged;
            GetMenuItem();
            var Comp = api.CompanyEntity;
            LedgerCache = Comp.GetCache(typeof(Uniconta.DataModel.GLAccount));
            DebtorCache = Comp.GetCache(typeof(Uniconta.DataModel.Debtor));
            CreditorCache = Comp.GetCache(typeof(Uniconta.DataModel.Creditor));
            localMenu.OnChecked += LocalMenu_OnChecked;
        }

        void DataControl_CurrentItemChanged(object sender, DevExpress.Xpf.Grid.CurrentItemChangedEventArgs e)
        {
            JournalLineGridClient oldselectedItem = e.OldItem as JournalLineGridClient;
            if (oldselectedItem != null)
            {
                oldselectedItem.PropertyChanged -= JournalLineGridClient_PropertyChanged;
                var Cache = GetCache(oldselectedItem._AccountType);
                if (Cache != null && Cache.Count > 1000)
                {
                    oldselectedItem.accntSource = null;
                    oldselectedItem.NotifyPropertyChanged("AccountSource");
                }
                Cache = GetCache(oldselectedItem._OffsetAccountType);
                if (Cache != null && Cache.Count > 1000)
                {
                    oldselectedItem.offsetAccntSource = null;
                    oldselectedItem.NotifyPropertyChanged("OffsetAccountSource");
                }
            }
            JournalLineGridClient selectedItem = e.NewItem as JournalLineGridClient;
            if (selectedItem != null)
                selectedItem.PropertyChanged += JournalLineGridClient_PropertyChanged;
        }

        void JournalLineGridClient_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var rec = sender as JournalLineGridClient;
            switch (e.PropertyName)
            {
                case "AccountType":
                    SetAccountSource(rec);
                    break;
                case "OffsetAccountType":
                    SetOffsetAccountSource(rec);
                    break;
            }
        }
        SQLCache LedgerCache, DebtorCache, CreditorCache;
        SQLCache GetCache(byte AccountType)
        {
            switch (AccountType)
            {
                case (byte)GLJournalAccountType.Finans:
                    return LedgerCache;
                case (byte)GLJournalAccountType.Debtor:
                    return DebtorCache;
                case (byte)GLJournalAccountType.Creditor:
                    return CreditorCache;
                default: return null;
            }
        }

        private void SetAccountSource(JournalLineGridClient rec)
        {
            var act = rec._AccountType;
            SQLCache cache = GetCache(act);
            if (cache != null)
            {
                int ver = cache.version + 10000 * (act + 1);
                if (ver != rec.AccountVersion || rec.accntSource == null)
                {
                    if (act == (byte)GLJournalAccountType.Finans)
                        rec.accntSource = new UnicontaClient.Pages.GLDailyJournalLine.LedgerSQLCacheFilter(cache);
                    else
                        rec.accntSource = cache.GetNotNullArray;
                    if (rec.accntSource != null)
                    {
                        rec.AccountVersion = ver;
                        rec.NotifyPropertyChanged("AccountSource");
                    }
                }
            }
        }

        private void SetOffsetAccountSource(JournalLineGridClient rec)
        {
            var act = rec._OffsetAccountType;
            SQLCache cache = GetCache(act);
            if (cache != null)
            {
                int ver = cache.version + 10000 * (act + 1);
                if (ver != rec.OffsetAccountVersion || rec.offsetAccntSource == null)
                {
                    if (act == (byte)GLJournalAccountType.Finans)
                        rec.offsetAccntSource = new UnicontaClient.Pages.GLDailyJournalLine.LedgerSQLCacheFilter(cache);
                    else
                        rec.offsetAccntSource = cache.GetNotNullArray;
                    if (rec.offsetAccntSource != null)
                    {
                        rec.OffsetAccountVersion = ver;
                        rec.NotifyPropertyChanged("OffsetAccountSource");
                    }
                }
            }
        }

        CorasauGridLookupEditorClient prevAccount;
        private void Account_GotFocus(object sender, RoutedEventArgs e)
        {
            JournalLineGridClient selectedItem = dgGldailyJournalLinesGrid.SelectedItem as JournalLineGridClient;
            if (selectedItem != null)
            {
                SetAccountSource(selectedItem);
                if (prevAccount != null)
                    prevAccount.isValidate = false;
                var editor = (CorasauGridLookupEditorClient)sender;
                prevAccount = editor;
                editor.isValidate = true;
            }
        }

        CorasauGridLookupEditorClient prevOffsetAccount;
        private void OffsetAccount_GotFocus(object sender, RoutedEventArgs e)
        {
            JournalLineGridClient selectedItem = dgGldailyJournalLinesGrid.SelectedItem as JournalLineGridClient;
            if (selectedItem != null)
            {
                SetOffsetAccountSource(selectedItem);
                if (prevOffsetAccount != null)
                    prevOffsetAccount.isValidate = false;
                var editor = (CorasauGridLookupEditorClient)sender;
                prevOffsetAccount = editor;
                editor.isValidate = true;
            }
        }

        private void Account_LostFocus(object sender, RoutedEventArgs e)
        {
            SetAccountByLookupText(sender, false);
        }
        private void OffsetAccount_LostFocus(object sender, RoutedEventArgs e)
        {
            SetAccountByLookupText(sender, true);
        }
        void SetAccountByLookupText(object sender, bool offsetAcc)
        {
            JournalLineGridClient selectedItem = dgGldailyJournalLinesGrid.SelectedItem as JournalLineGridClient;
            if (selectedItem == null)
                return;
            var actType = !offsetAcc ? selectedItem._AccountTypeEnum : selectedItem._OffsetAccountTypeEnum;
            if (actType != GLJournalAccountType.Finans)
                return;
            var le = sender as CorasauGridLookupEditor;
            if (string.IsNullOrEmpty(le.EnteredText))
                return;
            var accounts = LedgerCache?.GetNotNullArray as GLAccount[];
            if (accounts != null)
            {
                var act = accounts.Where(ac => ac._Lookup == le.EnteredText).FirstOrDefault();
                if (act != null)
                {
                    dgGldailyJournalLinesGrid.SetLoadedRow(selectedItem);
                    if (offsetAcc)
                        selectedItem.OffsetAccount = act.KeyStr;
                    else
                        selectedItem.Account = act.KeyStr;
                    le.EditValue = act.KeyStr;
                    dgGldailyJournalLinesGrid.SetModifiedRow(selectedItem);
                }
            }
            le.EnteredText = null;
        }

        bool isDataChanged = false;
        public override bool IsDataChaged { get { return isDataChanged; } }

        protected override async void LoadCacheInBackGround()
        {
            var api = this.api;
            var Comp = api.CompanyEntity;
            if (DebtorCache == null)
                DebtorCache = await api.LoadCache(typeof(Uniconta.DataModel.Debtor)).ConfigureAwait(false);
            if (CreditorCache == null)
                CreditorCache = await api.LoadCache(typeof(Uniconta.DataModel.Creditor)).ConfigureAwait(false);

            var header = this.masterRecord;
            if (header._Account != null)
            {
                var rec = GetCache((byte)header._DefaultAccountType)?.Get(header._Account);
                if (rec == null)
                    header._Account = null;
            }
            if (header._OffsetAccount != null)
            {
                var rec = GetCache((byte)header._DefaultOffsetAccountType)?.Get(header._OffsetAccount);
                if (rec == null)
                    header._OffsetAccount = null;
            }
        }

        void GetMenuItem()
        {
            RibbonBase rb = (RibbonBase)localMenu.DataContext;
            ibase = UtilDisplay.GetMenuCommandByName(rb, "Unlinked");
            var rbMenuAssignText = UtilDisplay.GetMenuCommandByName(rb, "AssignText");
            rbMenuAssignText.IsChecked = AssignText;
        }

        private void DgGldailyJournalLinesGrid_SelectedItemChanged(object sender, DevExpress.Xpf.Grid.SelectedItemChangedEventArgs e)
        {
            var gl = dgGldailyJournalLinesGrid.SelectedItem as GLDailyJournalLineClient;
            if (gl != null && gl._DocumentRef != 0)
            {
                var visibleRows = dgvoucherGrid.GetVisibleRows() as IList<VouchersClientLocal>;
                dgvoucherGrid.SelectedItem = visibleRows?.Where(v => v.PrimaryKeyId == gl._DocumentRef).FirstOrDefault();
            }
        }

        protected override void OnLayoutLoaded()
        {
            base.OnLayoutLoaded();
            SetDimensions();

            var DateVisible = (masterRecord._DateFunction == GLJournalDate.Free);
            var VoucherVisible = !masterRecord._GenerateVoucher;
            var TraceVisible = (masterRecord._TraceAccount != null);

            colDate.Visible = DateVisible;
            colDate.ShowInColumnChooser = !DateVisible;

            colVoucher.Visible = VoucherVisible;
            colVoucher.ShowInColumnChooser = !VoucherVisible;

            this.TraceBalance.Visible = TraceVisible;
            this.TraceBalance.ShowInColumnChooser = !TraceVisible;
        }

        void dgvoucherGrid_RowDoubleClick()
        {
            localMenu_OnItemClicked("Attach");
        }

        async void SaveAndRefresh(bool isAttached, UnicontaBaseEntity selectedItem)
        {
            busyIndicator.IsBusy = true;
            var err = await dgGldailyJournalLinesGrid.SaveData();
            if (err == ErrorCodes.Succes)
                dgGldailyJournalLinesGrid.UpdateItemSource(2, selectedItem);
            if (!isAttached)
                SetVoucherIsAttached();
            busyIndicator.IsBusy = false;
        }

        void localMenu_OnItemClicked(string ActionType)
        {
            var selectedVoucher = dgvoucherGrid.SelectedItem as VouchersClientLocal;
            var selectedJournalLine = dgGldailyJournalLinesGrid.SelectedItem as GLDailyJournalLineClient;

            switch (ActionType)
            {
                case "ViewPhysicalVoucher":
                case "ViewVoucher":
                    if (selectedVoucher == null)
                        return;
                    ViewVoucher(selectedVoucher);
                    break;
                case "ViewAttachedVoucher":
                    if (selectedJournalLine == null)
                        return;
                    ViewVoucher(selectedJournalLine);
                    break;
                case "Attach":
                    if (selectedVoucher == null || selectedJournalLine == null)
                        return;

                    var selectedRowId = selectedVoucher.RowId;
                    if (selectedRowId != 0)
                    {
                        dgGldailyJournalLinesGrid.SetLoadedRow(selectedJournalLine);
                        selectedJournalLine.DocumentRef = selectedRowId;
                        selectedJournalLine.DocumentDate = selectedVoucher._DocumentDate;
                        if (AssignText)
                            selectedJournalLine.Text = selectedJournalLine._Text ?? selectedVoucher._Text;
                        var amount = selectedJournalLine.Amount;
                        selectedJournalLine.Amount = amount != 0d ? amount : selectedVoucher._Amount;
                        dgGldailyJournalLinesGrid.SetModifiedRow(selectedJournalLine);
                        var visibleRows = dgvoucherGrid.GetVisibleRows() as IEnumerable<VouchersClientLocal>;
                        var voucher = visibleRows?.Where(v => v.PrimaryKeyId == selectedRowId).FirstOrDefault();
                        if (voucher != null)
                            voucher.IsAttached = true;
                        SaveAndRefresh(true, selectedJournalLine);
                    }
                    break;
                case "Detach":
                    if (selectedJournalLine == null)
                        return;
                    if (selectedJournalLine._DocumentRef != 0)
                    {
                        dgGldailyJournalLinesGrid.SetLoadedRow(selectedJournalLine);
                        selectedJournalLine.DocumentRef = 0;
                        dgGldailyJournalLinesGrid.SetModifiedRow(selectedJournalLine);
                        SaveAndRefresh(false, selectedJournalLine);
                    }
                    else
                        UnicontaMessageBox.Show(Uniconta.ClientTools.Localization.lookup("NoVoucherExist"), Uniconta.ClientTools.Localization.lookup("Information"), MessageBoxButton.OK);
                    break;
                case "Unlinked":
                      SetUnlinkedAndLinkedAll();
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
        }

        private void LocalMenu_OnChecked(string ActionType, bool IsChecked)
        {
            switch (ActionType)
            {
                case "AssignText":
                    AssignText = IsChecked;
                    break;
            }
        }
        private void SetUnlinkedAndLinkedAll()
        {
            if (ibase == null)
                return;
            if (ibase.Caption == Uniconta.ClientTools.Localization.lookup("Unlinked"))
            {
                Unlinked(true);
                ibase.Caption = Uniconta.ClientTools.Localization.lookup("All");
                ibase.LargeGlyph = Utilities.Utility.GetGlyph("Account-statement_32x32.png");
            }
            else if (ibase.Caption == Uniconta.ClientTools.Localization.lookup("All"))
            {
                Unlinked(false);
                ibase.Caption = Uniconta.ClientTools.Localization.lookup("Unlinked");
                ibase.LargeGlyph = Utilities.Utility.GetGlyph("Check_Journal_32x32.png");
            }
        }

        void Unlinked(bool unlink)
        {
            if (unlink)
            {
                dgvoucherGrid.MergeColumnFilters("([IsAttached] = False)");
                dgGldailyJournalLinesGrid.MergeColumnFilters("([HasVoucher] = False)");
            }
            else
            {
                dgvoucherGrid.ClearColumnFilter("IsAttached");
                dgGldailyJournalLinesGrid.ClearColumnFilter("HasVoucher");
            }
        }

        private void ViewVoucher(UnicontaBaseEntity baseEntity)
        {
            busyIndicator.IsBusy = true;
            string header = null;
            if (baseEntity is VouchersClientLocal)
            {
                header = string.Format("{0} {1}", Uniconta.ClientTools.Localization.lookup("View"), Uniconta.ClientTools.Localization.lookup("PhysicalVoucher"));
                ViewVoucher(TabControls.VouchersPage3, dgvoucherGrid.syncEntity, header);
            }
            else if (baseEntity is GLDailyJournalLineClient)
            {
                header = string.Format("{0} {1}", Uniconta.ClientTools.Localization.lookup("View"), Uniconta.ClientTools.Localization.lookup("AttachedVoucher"));
                ViewVoucher(TabControls.VouchersPage3, dgGldailyJournalLinesGrid.syncEntity, header);
            }
            busyIndicator.IsBusy = false;
        }

        public async override Task InitQuery()
        {
            busyIndicator.IsBusy = true;
            await dgvoucherGrid.Filter(null);
            await dgGldailyJournalLinesGrid.Filter(null);
            SetVoucherIsAttached();
            busyIndicator.IsBusy = false;
        }

        private void SetVoucherIsAttached()
        {
            var rows = dgGldailyJournalLinesGrid.ItemsSource as List<JournalLineGridClient>;
            var docRefs = rows.Select(r => r.DocumentRef).Distinct();
            var vouchers = dgvoucherGrid.ItemsSource as List<VouchersClientLocal>;
            foreach (var voucher in vouchers)
            {
                if (docRefs.Contains(voucher.PrimaryKeyId))
                    voucher.IsAttached = true;
                else
                    voucher.IsAttached = false;
            }
        }

        private void SetDimensions()
        {
            var c = api.CompanyEntity;
            UnicontaClient.Utilities.Utility.SetDimensionsGrid(api, coldim1, coldim2, coldim3, coldim4, coldim5);
            UnicontaClient.Utilities.Utility.SetDimensionsGrid(api, clDim1, clDim2, clDim3, clDim4, clDim5);
        }

        public override void AssignMultipleGrid(List<CorasauDataGrid> gridCtrls)
        {
            gridCtrls.Add(dgGldailyJournalLinesGrid);
            gridCtrls.Add(dgvoucherGrid);
        }

        public override string NameOfControl
        {
            get { return TabControls.MatchPhysicalVoucherToGLDailyJournalLines; }
        }
    }

    public class VouchersClientLocal : VouchersClient
    {
        bool isAttached;
        [Display(Name = "Attached", ResourceType = typeof(VouchersClientText))]
        public bool IsAttached { get { return isAttached; } set { isAttached = value; NotifyPropertyChanged("IsAttached"); } }
    }
}
