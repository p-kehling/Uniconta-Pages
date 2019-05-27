using Uniconta.API.Service;
using UnicontaClient.Models;
using UnicontaClient.Utilities;
using Uniconta.ClientTools;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.ClientTools.Util;
using Uniconta.Common;
using DevExpress.Xpf.Editors.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Collections;
using System.IO;
using Uniconta.DataModel;
using Uniconta.ClientTools.Controls;
using Uniconta.API.DebtorCreditor;
using System.Windows.Threading;
using DevExpress.Xpf.Grid;
using UnicontaClient.Controls.Dialogs;
using Uniconta.API.DebtorCreditor;
using Microsoft.Win32;
using UnicontaClient.Controls.Dialogs;
#if !SILVERLIGHT
using UnicontaClient.Pages;
#endif

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public partial class QuickInvoice : GridBasePage
    {
        public override string NameOfControl
        {
            get { return TabControls.QuickInvoice.ToString(); }
        }

        public QuickInvoice(BaseAPI API)
            : base(API, string.Empty)
        {
            InitializeComponent();
            InitPage();
        }

        SQLCache items, warehouse, creditors, standardVariants, variants1, variants2, ProjectCache;
        CreditorOrderClient Order;
        byte OrderCurrency;
        byte CompCurrency;
        double exchangeRate;
        Uniconta.API.DebtorCreditor.FindPrices PriceLookup;
        CreditorOrderClient initialOrder;

        private void InitPage()
        {
            InitializeComponent();
            var api = this.api;
            var Comp = api.CompanyEntity;
            CompCurrency = (byte)Comp._CurrencyId;

            ((TableView)dgCreditorOrderLineGrid.View).RowStyle = Application.Current.Resources["StyleRow"] as Style;
            ledim1.api = ledim2.api = ledim3.api = ledim4.api = ledim5.api = LeAccount.api =
                lePayment.api = leTransType.api = LePostingAccount.api = leShipment.api = leDeliveryTerm.api =
                    Projectlookupeditor.api = PrCategorylookupeditor.api = employeelookupeditor.api = api;

            initialOrder = new CreditorOrderClient();
            initialOrder._DeliveryCountry = Comp._CountryId;
            initialOrder.SetMaster(Comp);

            setDimensions();
            cbDeliveryCountry.ItemsSource = Enum.GetValues(typeof(Uniconta.Common.CountryCode));
            SetRibbonControl(localMenu, dgCreditorOrderLineGrid);
            dgCreditorOrderLineGrid.api = api;
            dgCreditorOrderLineGrid.BusyIndicator = busyIndicator;
            localMenu.OnItemClicked += localMenu_OnItemClicked;
            dgCreditorOrderLineGrid.View.DataControl.CurrentItemChanged += DataControl_CurrentItemChanged;
#if !SILVERLIGHT
            txtDelZipCode.EditValueChanged += TxtDelZipCode_EditValueChanged;
            btnAccount.ToolTip = string.Format(Uniconta.ClientTools.Localization.lookup("CreateOBJ"), Uniconta.ClientTools.Localization.lookup("Creditor"));
#endif
            txtName.IsEnabled = false;
            dgCreditorOrderLineGrid.Visibility = Visibility.Visible;
            ShowCustomCloseConfiramtion = true;

            // we setup first order
            ClearFields(initialOrder);

            if (dgCreditorOrderLineGrid.IsLoadedFromLayoutSaved)
            {
                dgCreditorOrderLineGrid.ClearSorting();
                dgCreditorOrderLineGrid.IsLoadedFromLayoutSaved = false;
            }

            this.creditors = Comp.GetCache(typeof(Uniconta.DataModel.Creditor));
            this.items = Comp.GetCache(typeof(InvItem));
            this.warehouse = Comp.GetCache(typeof(InvWarehouse));
            this.variants1 = Comp.GetCache(typeof(InvVariant1));
            this.variants2 = Comp.GetCache(typeof(InvVariant2));
            this.standardVariants = Comp.GetCache(typeof(InvStandardVariant));
            RemoveMenuItem();
            dgCreditorOrderLineGrid.allowSave = false;
#if SILVERLIGHT
            Application.Current.RootVisual.KeyDown += Page_KeyDown;
#else
            this.KeyDown += Page_KeyDown;
#endif
        }

        void RemoveMenuItem()
        {
            RibbonBase rb = (RibbonBase)localMenu.DataContext;
            var Comp = api.CompanyEntity;

#if !SILVERLIGHT
            if (Comp._CountryId != CountryCode.Denmark)
                UtilDisplay.RemoveMenuCommand(rb, "ReadOIOUBL");
#else
            UtilDisplay.RemoveMenuCommand(rb, "ReadOIOUBL");
#endif
        }


        private void Page_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F8)
                localMenu_OnItemClicked("AddItems");

            if (e.Key == Key.F9)
            {
                dgCreditorOrderLineGrid.tableView.FocusedRowHandle = 0;
                dgCreditorOrderLineGrid.tableView.ShowEditor();
            }
        }

        public override Task InitQuery() { return null; }

        private async void TxtDelZipCode_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            var s = sender as TextEditor;
            if (s != null && s.IsLoaded)
            {
                var deliveryCountry = Order.DeliveryCountry ?? api.CompanyEntity._CountryId;
                var city = await UtilDisplay.GetCityName(s.Text, deliveryCountry);
                if (city != null)
                    Order.DeliveryCity = city;
            }
        }

        protected override void OnLayoutLoaded()
        {
            base.OnLayoutLoaded();
            var company = api.CompanyEntity;
            if (!company.Location || !company.Warehouse)
                Location.Visible = Location.ShowInColumnChooser = false;
            if (!company.Warehouse)
                Warehouse.Visible = Warehouse.ShowInColumnChooser = false;
            if (!company.Warehouse)
                Warehouse.Visible = Warehouse.ShowInColumnChooser = false;
            if (!company.DeliveryAddress)
                delAddNavBar.IsVisible = false;
            if (!company.Shipments)
            {
                tbShipment.Visibility = Visibility.Collapsed;
                leShipment.Visibility = Visibility.Collapsed;
            }
            if (!company.ProjectTask)
            {
                Task.Visible = false;
                Task.ShowInColumnChooser = false;
            }


            Utility.SetupVariants(api, colVariant, colVariant1, colVariant2, colVariant3, colVariant4, colVariant5, Variant1Name, Variant2Name, Variant3Name, Variant4Name, Variant5Name);
            Utility.SetDimensionsGrid(api, cldim1, cldim2, cldim3, cldim4, cldim5);
        }

        public override void Utility_Refresh(string screenName, object argument)
        {
            if (screenName == TabControls.InvItemStoragePage && argument != null)
            {
                var storeloc = argument as InvItemStorageClient;
                if (storeloc == null) return;
                var selected = dgCreditorOrderLineGrid.SelectedItem as CreditorOrderLineClient;
                if (selected == null) return;
                dgCreditorOrderLineGrid.SetLoadedRow(selected);
                selected.Warehouse = storeloc.Warehouse;
                selected.Location = storeloc.Location;
                dgCreditorOrderLineGrid.SetModifiedRow(selected);
            }

            if (screenName == TabControls.AddMultipleInventoryItem)
            {
                var param = argument as object[];
                if (param != null)
                {
                    var invItems = param[0] as List<UnicontaBaseEntity>;
                    var iSource = dgCreditorOrderLineGrid.ItemsSource as IEnumerable<CreditorOrderLineClient>;
                    var nonemptyList = new List<UnicontaBaseEntity>();
                    if (iSource != null)
                    {
                        foreach (var row in iSource)
                        {
                            if (row._Item != null || row._Text != null || row._Note != null)
                                nonemptyList.Add(row);
                        }
                        dgCreditorOrderLineGrid.SetSource(nonemptyList.ToArray());
                    }
                    dgCreditorOrderLineGrid.PasteRows(invItems);
                }
            }

            if (screenName == TabControls.AttachVoucherGridPage && argument != null)
            {
                var voucherObj = argument as object[];
                var voucher = voucherObj[0] as VouchersClient;
                if (voucher != null)
                    Order.DocumentRef = voucher.RowId;
            }
        }

        public override bool IsDataChaged
        {
            get
            {
                var lst = dgCreditorOrderLineGrid.ItemsSource as IEnumerable<DCOrderLine>;
                if (lst == null || !lst.Any())
                    return false;
                if (lst.Count() > 1)
                    return true;
                var lin = lst.First();
                return lin._Item != null || lin._Qty != 0 || lin._Amount != 0;
            }
        }

        private void DataControl_CurrentItemChanged(object sender, DevExpress.Xpf.Grid.CurrentItemChangedEventArgs e)
        {
            var oldselectedItem = e.OldItem as CreditorOrderLineClient;
            if (oldselectedItem != null)
                oldselectedItem.PropertyChanged -= SelectedItem_PropertyChanged;
            var selectedItem = e.NewItem as CreditorOrderLineClient;
            if (selectedItem != null)
            {
                selectedItem.PropertyChanged += SelectedItem_PropertyChanged;
                if (selectedItem.Variant1Source == null)
                    setVariant(selectedItem, false);
                if (selectedItem.Variant2Source == null)
                    setVariant(selectedItem, true);
            }
        }
        async void setVariant(CreditorOrderLineClient rec, bool SetVariant2)
        {
            if (items == null || variants1 == null || variants2 == null)
                return;

            //Check for Variant2 Exist
            if (api.CompanyEntity.NumberOfVariants < 2)
                SetVariant2 = false;

            var item = (InvItem)items.Get(rec._Item);
            if (item != null && item._StandardVariant != null)
            {
                rec.standardVariant = item._StandardVariant;
                var master = (InvStandardVariant)standardVariants?.Get(item._StandardVariant);
                if (master == null)
                    return;
                if (master._AllowAllCombinations)
                {
                    rec.Variant1Source = this.variants1?.GetKeyStrRecords.Cast<InvVariant1>();
                    rec.Variant2Source = this.variants2?.GetKeyStrRecords.Cast<InvVariant2>();
                }
                else
                {
                    var Combinations = master.Combinations ?? await master.LoadCombinations(api);
                    if (Combinations == null)
                        return;
                    List<InvVariant1> invs1 = null;
                    List<InvVariant2> invs2 = null;
                    string vr1 = null;
                    if (SetVariant2)
                    {
                        vr1 = rec._Variant1;
                        invs2 = new List<InvVariant2>();
                    }
                    else
                        invs1 = new List<InvVariant1>();

                    string LastVariant = null;
                    var var2Value = rec._Variant2;
                    bool hasVariantValue = (var2Value == null);
                    foreach (var cmb in Combinations)
                    {
                        if (SetVariant2)
                        {
                            if (cmb._Variant1 == vr1 && cmb._Variant2 != null)
                            {
                                var v2 = (InvVariant2)variants2.Get(cmb._Variant2);
                                invs2.Add(v2);
                                if (var2Value == v2._Variant)
                                    hasVariantValue = true;

                            }
                        }
                        else if (LastVariant != cmb._Variant1)
                        {
                            LastVariant = cmb._Variant1;
                            var v1 = (InvVariant1)variants1.Get(cmb._Variant1);
                            invs1.Add(v1);
                        }
                    }
                    if (SetVariant2)
                    {
                        rec.variant2Source = invs2;
                        if (!hasVariantValue)
                            rec.Variant2 = null;
                    }
                    else
                        rec.variant1Source = invs1;
                }
            }
            else
            {
                rec.variant1Source = null;
                rec.variant2Source = null;
            }
            if (SetVariant2)
                rec.NotifyPropertyChanged("Variant2Source");
            else
                rec.NotifyPropertyChanged("Variant1Source");
        }
        async void setLocation(InvWarehouse master, CreditorOrderLineClient rec)
        {
            if (api.CompanyEntity.Location)
            {
                if (master != null)
                    rec.locationSource = master.Locations ?? await master.LoadLocations(api);
                else
                {
                    rec.locationSource = null;
                    rec.Location = null;
                }
                rec.NotifyPropertyChanged("LocationSource");
            }
        }

        private void SelectedItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var rec = sender as CreditorOrderLineClient;
            switch (e.PropertyName)
            {
                case "Item":
                    var selectedItem = (InvItem)items?.Get(rec._Item);
                    if (selectedItem != null)
                    {
                        if (selectedItem._AlternativeItem != null &&
                            selectedItem._UseAlternative == UseAlternativeItem.Always)
                        {
                            var altItem = (InvItem)items.Get(selectedItem._AlternativeItem);
                            if (altItem != null && altItem._AlternativeItem == null)
                            {
                                rec.Item = selectedItem._AlternativeItem;
                                return;
                            }
                        }
                        var Comp = api.CompanyEntity;
                        if (selectedItem._PurchaseQty != 0d)
                            rec.Qty = selectedItem._PurchaseQty;
                        else if (Comp._PurchaseLineOne)
                            rec.Qty = 1d;
                        rec.SetItemValues(selectedItem, Comp._PurchaseLineStorage);
                        if (this.PriceLookup != null)
                            this.PriceLookup.SetPriceFromItem(rec, selectedItem);
                        else if (selectedItem._PurchasePrice != 0 && Comp.SameCurrency(selectedItem._PurchaseCurrency, (byte)this.Order._Currency))
                            rec.Price = selectedItem._PurchasePrice;
                        else
                            rec.Price = (exchangeRate == 0d) ? selectedItem._CostPrice : Math.Round(selectedItem._CostPrice * exchangeRate, 2);
                        if (selectedItem._StandardVariant != rec.standardVariant)
                        {
                            rec.Variant1 = null;
                            rec.Variant2 = null;
                            rec.variant2Source = null;
                            rec.NotifyPropertyChanged("Variant2Source");
                        }
                        setVariant(rec, false);
                        TableField.SetUserFieldsFromRecord(selectedItem, rec);
                        if (selectedItem._Blocked)
                            UtilDisplay.ShowErrorCode(ErrorCodes.ItemIsOnHold, null);
                    }
                    break;
                case "Qty":
                    if (this.PriceLookup != null && this.PriceLookup.UseCustomerPrices)
                        this.PriceLookup.GetCustomerPrice(rec, false);
                    break;
                case "Warehouse":
                    if (warehouse != null)
                    {
                        var selected = (InvWarehouse)warehouse.Get(rec._Warehouse);
                        setLocation(selected, (CreditorOrderLineClient)rec);
                    }
                    break;
                case "Location":
                    if (string.IsNullOrEmpty(rec._Warehouse))
                        rec._Location = null;
                    break;
                case "EAN":
                    DebtorOfferLines.FindOnEAN(rec, this.items, api);
                    break;
                case "Total":
                    RecalculateAmount();
                    break;
                case "Variant1":
                    if (rec._Variant1 != null)
                        setVariant(rec, true);
                    if (this.PriceLookup != null && this.PriceLookup.UseCustomerPrices)
                        this.PriceLookup.GetCustomerPrice(rec, false);
                    break;
                case "Variant2":
                case "Variant3":
                case "Variant4":
                case "Variant5":
                    if (this.PriceLookup != null && this.PriceLookup.UseCustomerPrices)
                        this.PriceLookup.GetCustomerPrice(rec, false);
                    break;
                case "Project":
                    if (ProjectCache != null)
                    {
                        var selected = (ProjectClient)ProjectCache.Get(rec._Project);
                        setTask(selected, rec);
                    }
                    break;
                case "Task":
                    if (string.IsNullOrEmpty(rec._Project))
                        rec._Task = null;
                    break;
            }
        }

        void RecalculateAmount()
        {
            var ret = DebtorOfferLines.RecalculateLineSum((IList)dgCreditorOrderLineGrid.ItemsSource, this.exchangeRate);
            double Amountsum = ret.Item1;
            double Costsum = ret.Item2;
            double AmountsumCompCur = ret.Item3;
            RibbonBase rb = (RibbonBase)localMenu.DataContext;
            var groups = UtilDisplay.GetMenuCommandsByStatus(rb, true);
            foreach (var grp in groups)
                grp.StatusValue = Amountsum.ToString("N2");
        }

        private async void LookRate(DCOrderLineClient rec, double price, byte From, byte To)
        {
            var Rate = await api.session.ExchangeRate((Currencies)From, (Currencies)To,
                BasePage.GetSystemDefaultDate(), api.CompanyEntity);
            rec.Price = (Rate == 0d) ? price : Math.Round(price * Rate, 2);
        }

        VouchersClient attachedVoucher;

        private void localMenu_OnItemClicked(string ActionType)
        {
            switch (ActionType)
            {
                case "AddRow":
                    var row = dgCreditorOrderLineGrid.AddRow() as DCOrderLine;
                    row._ExchangeRate = this.exchangeRate;
                    break;
                case "CopyRow":
                    dgCreditorOrderLineGrid.CopyRow();
                    break;
                case "DeleteRow":
                    dgCreditorOrderLineGrid.DeleteRow();
                    break;
                case "Storage":
                    AddDockItem(TabControls.InvItemStoragePage, dgCreditorOrderLineGrid.syncEntity, true);
                    break;
                case "GenerateInvoice":
                    if (!string.IsNullOrEmpty(Order.Account))
                    {
                        if (Utility.HasControlRights("GenerateInvoice", api.CompanyEntity))
                            GenerateInvoice(Order);
                        else
                            UtilDisplay.ShowControlAccessMsg("GenerateInvoice");
                    }
                    else
                    {
                        string strmg = string.Format(Uniconta.ClientTools.Localization.lookup("CannotBeBlank"),
                            Uniconta.ClientTools.Localization.lookup("Account"));
                        UnicontaMessageBox.Show(strmg, Uniconta.ClientTools.Localization.lookup("Error"),
                            MessageBoxButton.OK);
                    }
                    break;
                case "RefVoucher":
                    var _refferedVouchers = new List<int>();
                    if (Order._DocumentRef != 0)
                        _refferedVouchers.Add(Order._DocumentRef);

                    AddDockItem(TabControls.AttachVoucherGridPage, new object[1] { _refferedVouchers }, true);
                    break;
                case "ViewVoucher":
                    busyIndicator.IsBusy = true;
                    ViewVoucher(TabControls.VouchersPage3, Order);
                    busyIndicator.IsBusy = false;
                    break;

                case "ImportVoucher":
                    if (Order == null)
                        return;
                    VouchersClient voucher = new VouchersClient();
                    voucher._Content = ContentTypes.PurchaseInvoice;
                    voucher._Amount = Order.InvoiceAmount;
                    voucher._CreditorAccount = Order._DCAccount;
                    CWAddVouchers addVouvhersDialog = new CWAddVouchers(api, voucher: voucher);
                    addVouvhersDialog.Closed += delegate
                    {
                        if (addVouvhersDialog.DialogResult == true)
                        {
                            if (addVouvhersDialog.VoucherRowIds.Count() > 0)
                            {
                                Order.DocumentRef = addVouvhersDialog.VoucherRowIds[0];
                                Order.InvoiceAmount = addVouvhersDialog.vouchersClient._Amount;
                                Order.InvoiceNumber = addVouvhersDialog.vouchersClient.Invoice;
                            }
                        }
                    };
                    addVouvhersDialog.Show();
                    break;

                case "RemoveVoucher":
                    RemoveVoucher(Order);
                    break;
                case "InsertSubTotal":
                    var dbOrderLineClient = new CreditorOrderLineClient { Subtotal = true };
                    dgCreditorOrderLineGrid.AddRow(dbOrderLineClient);
                    break;
                case "ReadOIOUBL":
                    ReadOIOUBL();
                    break;
                case "StockLines":
                    if (dgCreditorOrderLineGrid.SelectedItem == null) return;

                    var credOrderLine = dgCreditorOrderLineGrid.SelectedItem as CreditorOrderLineClient;
                    if (!string.IsNullOrEmpty(credOrderLine._Item))
                        AddDockItem(TabControls.CreditorInvoiceLine, credOrderLine, string.Format("{0}: {1}", Uniconta.ClientTools.Localization.lookup("InvTransaction"), credOrderLine._Item));
                    break;
                case "AttachDoc":
                    AttachDocuments();
                    break;
                case "AddItems":
                    if (this.items == null)
                        return;
                    object[] paramArray = new object[3] { new InvItemPurchaseCacheFilter(this.items), dgCreditorOrderLineGrid.TableTypeUser, Order };
                    AddDockItem(TabControls.AddMultipleInventoryItem, paramArray, true,
                        string.Format(Uniconta.ClientTools.Localization.lookup("AddOBJ"), Uniconta.ClientTools.Localization.lookup("InventoryItems")), null, floatingLoc: Utility.GetDefaultLocation());
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
            RecalculateAmount();
        }

        public override void RowsPastedDone() { RecalculateAmount(); }

        public override void RowPasted(UnicontaBaseEntity rec)
        {
            var orderLine = (CreditorOrderLineClient)rec;
            if (orderLine._Item != null)
            {
                var selectedItem = (InvItem)items.Get(orderLine._Item);
                if (selectedItem != null)
                {
                    PriceLookup?.SetPriceFromItem(orderLine, selectedItem);
                    orderLine.SetItemValues(selectedItem, 0);
                    TableField.SetUserFieldsFromRecord(selectedItem, orderLine);
                }
                else
                    orderLine._Item = null;
            }
        }

        void ClearFields(CreditorOrderClient initialOrder)
        {
            Order = StreamingManager.Clone(initialOrder) as CreditorOrderClient;
            this.DataContext = Order;
            dgCreditorOrderLineGrid.UpdateMaster(Order);
            dgCreditorOrderLineGrid.ItemsSource = null;
            dgCreditorOrderLineGrid.AddFirstRow();
            LeAccount.Focus();
        }

        static bool showInvoice = true;

        private void GenerateInvoice(CreditorOrderClient dbOrder)
        {
            IEnumerable<DCOrderLineClient> lines = (IEnumerable<DCOrderLineClient>)dgCreditorOrderLineGrid.ItemsSource;
            if (lines == null || lines.Count() == 0)
                return;
            var dc = dbOrder.Creditor;
            if (dc == null)
                return;
            if (!Utility.IsExecuteWithBlockedAccount(dc)) return;

            if (!api.CompanyEntity.SameCurrency(dbOrder._Currency, dc._Currency))
            {
                var confirmationMsgBox = UnicontaMessageBox.Show(string.Format("{0}.\n{1}", string.Format(Uniconta.ClientTools.Localization.lookup("CurrencyMismatch"), dc.Currency, dbOrder.Currency),
                Uniconta.ClientTools.Localization.lookup("ProceedConfirmation")), Uniconta.ClientTools.Localization.lookup("Confirmation"), MessageBoxButton.OKCancel);
                if (confirmationMsgBox != MessageBoxResult.OK)
                    return;
            }

            CWGenerateInvoice GenrateInvoiceDialog = new CWGenerateInvoice(true, string.Empty, true);
#if !SILVERLIGHT
            GenrateInvoiceDialog.DialogTableId = 2000000004;
#endif
            if (dbOrder._InvoiceNumber != 0)
                GenrateInvoiceDialog.SetInvoiceNumber(dbOrder._InvoiceNumber);
            if (dbOrder._InvoiceDate != DateTime.MinValue)
                GenrateInvoiceDialog.SetInvoiceDate(dbOrder._InvoiceDate);
            GenrateInvoiceDialog.SetInvPrintPreview(showInvoice);
            GenrateInvoiceDialog.Closed += async delegate
            {
                if (GenrateInvoiceDialog.DialogResult == true)
                {
                    showInvoice = GenrateInvoiceDialog.ShowInvoice || GenrateInvoiceDialog.InvoiceQuickPrint;
                    if (!GenrateInvoiceDialog.IsSimulation && GenrateInvoiceDialog.InvoiceNumber == 0)
                    {
                        UtilDisplay.ShowErrorCode(ErrorCodes.InvoiceNumberMissing);
                        return;
                    }

                    var invoicePostingResult = new InvoicePostingPrintGenerator(api, this, dbOrder, lines, GenrateInvoiceDialog.GenrateDate, GenrateInvoiceDialog.InvoiceNumber, GenrateInvoiceDialog.IsSimulation,
                        CompanyLayoutType.PurchaseInvoice, showInvoice, GenrateInvoiceDialog.InvoiceQuickPrint, GenrateInvoiceDialog.NumberOfPages, false, GenrateInvoiceDialog.Emails, GenrateInvoiceDialog.sendOnlyToThisEmail,
                        false, false, documents);

                    busyIndicator.BusyContent = Uniconta.ClientTools.Localization.lookup("GeneratingPage");
                    busyIndicator.IsBusy = true;
                    var result = await invoicePostingResult.Execute();
                    busyIndicator.IsBusy = false;

                    if (result)
                    {
                        if (!GenrateInvoiceDialog.IsSimulation)
                        {
                            dgCreditorOrderLineGrid.ItemsSource = new List<CreditorOrderLineClient>();
                            documents = null;
                            if (attachDocMenu != null)
                                attachDocMenu.Caption = string.Format(Uniconta.ClientTools.Localization.lookup("AttachOBJ"), Uniconta.ClientTools.Localization.lookup("Documents"));

                        }

                        if (invoicePostingResult.PostingResult.Header._InvoiceNumber != 0)
                        {
                            var msg = string.Format(Uniconta.ClientTools.Localization.lookup("InvoiceHasBeenGenerated"), invoicePostingResult.PostingResult.Header._InvoiceNumber);
                            msg = string.Format("{0}{1}{2} {3}", msg, Environment.NewLine, Uniconta.ClientTools.Localization.lookup("LedgerVoucher"), invoicePostingResult.PostingResult.Header._Voucher);
                            UnicontaMessageBox.Show(msg, Uniconta.ClientTools.Localization.lookup("Message"), MessageBoxButton.OK);

                            if (attachedVoucher != null)
                            {
                                attachedVoucher.PurchaseNumber = Order._OrderNumber;
                                api.UpdateNoResponse(attachedVoucher);
                            }
                            ClearFields(initialOrder);
                        }
                    }
                    else
                        Utility.ShowJournalError(invoicePostingResult.PostingResult.ledgerRes, dgCreditorOrderLineGrid);
                }
            };
            GenrateInvoiceDialog.Show();
        }

        void leAccount_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            if (readingOIOUBL)
                return;
            string id = Convert.ToString(e.NewValue);
            var crdtor = (Uniconta.DataModel.Creditor)creditors?.Get(id);
            if (crdtor != null)
            {
                Order.Account = crdtor._Account;
                Order.SetCurrency(crdtor._Currency);
                Order.DeliveryName = crdtor._DeliveryName;
                Order.DeliveryAddress1 = crdtor._DeliveryAddress1;
                Order.DeliveryAddress2 = crdtor._DeliveryAddress2;
                Order.DeliveryAddress3 = crdtor._DeliveryAddress3;
                Order.DeliveryZipCode = crdtor._DeliveryZipCode;
                Order.DeliveryCity = crdtor._DeliveryCity;
                if (crdtor._DeliveryCountry != 0)
                    Order.DeliveryCountry = crdtor._DeliveryCountry;
                Order.Payment = crdtor._Payment;
                Order.EndDiscountPct = crdtor._EndDiscountPct;
                Order.PostingAccount = crdtor._PostingAccount;
                Order._PaymentMethod = crdtor._PaymentMethod;
                Order.Shipment = crdtor._Shipment;
                Order.DeliveryTerm = crdtor._DeliveryTerm;

                OrderCurrency = (byte)Order._Currency;
                if (OrderCurrency != 0 && OrderCurrency != CompCurrency)
                    loadExchange();

                IEnumerable<DCOrderLineClient> lines = (IEnumerable<DCOrderLineClient>)dgCreditorOrderLineGrid.ItemsSource;
                lines?.FirstOrDefault()?.SetMaster(Order);
                PriceLookup?.OrderChanged(Order);
                api.Read(crdtor);
            }
        }

        async void UpdateVoucher(CreditorOrderClient selectedItem)
        {
            busyIndicator.IsBusy = true;
            var err = await api.Update(selectedItem);
            busyIndicator.IsBusy = false;
            if (err != ErrorCodes.Succes)
                UtilDisplay.ShowErrorCode(err);
        }

        bool readingOIOUBL;
        public async void ReadOIOUBL()
        {
            readingOIOUBL = true;
#if !SILVERLIGHT
            try
            {
                var sfd = UtilDisplay.LoadOpenFileDialog;
                sfd.Filter = UtilFunctions.GetFilteredExtensions(FileextensionsTypes.XML);

                var userClickedSave = sfd.ShowDialog();
                if (userClickedSave != true)
                    return;

                using (var stream = File.Open(sfd.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var sr = new StreamReader(stream);
                    var oioublText = await sr.ReadToEndAsync();

                    var creditors2 = (Uniconta.DataModel.Creditor[])creditors?.GetNotNullArray;
                    var order = await OIOUBL.ReadInvoiceCreditNoteOrOrder(oioublText, creditors2, api, true);

                    if (order == null)
                    {
                        ClearFields(initialOrder);
                        return;
                    }

                    order.SetMaster(api.CompanyEntity);
                    //PriceLookup?.OrderChanged(order);

                    if (order != null)
                    {
                        var orderLines = order.Lines;
                        order.Lines = null;

                        ClearFields(order);

                        int countLine = 0;
                        foreach (var line in orderLines)
                        {
                            line.SetMaster(order);
                            line._LineNumber = ++countLine;
                        }

                        dgCreditorOrderLineGrid.ItemsSource = orderLines;

                        if (order.DocumentRef != 0)
                            UpdateVoucher(Order);
                    }
                }
                readingOIOUBL = false;
            }
            catch (Exception e)
            {
                readingOIOUBL = false;
                if (e.StackTrace.ToLower().Contains("xmlserializer"))
                    UnicontaMessageBox.Show("The file is not a valid XSD schemas. For more information (validation info) use www.oioubl.net/validator/", Uniconta.ClientTools.Localization.lookup("Information"));
                else
                    UnicontaMessageBox.Show(e.Message, Uniconta.ClientTools.Localization.lookup("Information"));
            }
#endif
        }

        CorasauGridLookupEditorClient prevLocation;

        private void Location_GotFocus(object sender, RoutedEventArgs e)
        {
            CreditorOrderLineClient selectedItem =
                dgCreditorOrderLineGrid.SelectedItem as CreditorOrderLineClient;
            if (selectedItem != null && selectedItem._Warehouse != null && warehouse != null)
            {
                var selected = (InvWarehouse)warehouse.Get(selectedItem._Warehouse);
                setLocation(selected, selectedItem);
                if (prevLocation != null)
                    prevLocation.isValidate = false;
                var editor = (CorasauGridLookupEditorClient)sender;
                prevLocation = editor;
                editor.isValidate = true;
            }
        }

        CorasauGridLookupEditorClient prevVariant1;

        private void variant1_GotFocus(object sender, RoutedEventArgs e)
        {
            if (prevVariant1 != null)
                prevVariant1.isValidate = false;
            var editor = (CorasauGridLookupEditorClient)sender;
            prevVariant1 = editor;
            editor.isValidate = true;
        }

        CorasauGridLookupEditorClient prevVariant2;

        private void variant2_GotFocus(object sender, RoutedEventArgs e)
        {
            if (prevVariant2 != null)
                prevVariant2.isValidate = false;
            var editor = (CorasauGridLookupEditorClient)sender;
            prevVariant2 = editor;
            editor.isValidate = true;
        }

        protected override async void LoadCacheInBackGround()
        {
            var api = this.api;
            var Comp = api.CompanyEntity;

            if (this.creditors == null)
                this.creditors = Comp.GetCache(typeof(Uniconta.DataModel.Creditor)) ?? await Comp.LoadCache(typeof(Uniconta.DataModel.Creditor), api).ConfigureAwait(false);

            if (this.items == null)
                this.items = Comp.GetCache(typeof(Uniconta.DataModel.InvItem)) ?? await Comp.LoadCache(typeof(Uniconta.DataModel.InvItem), api).ConfigureAwait(false);

            if (Comp.Warehouse && this.warehouse == null)
                this.warehouse = await Comp.LoadCache(typeof(Uniconta.DataModel.InvWarehouse), api).ConfigureAwait(false);

            if (Comp.ItemVariants)
            {
                if (this.standardVariants == null)
                    this.standardVariants = Comp.GetCache(typeof(Uniconta.DataModel.InvStandardVariant)) ?? await Comp.LoadCache(typeof(Uniconta.DataModel.InvStandardVariant), api).ConfigureAwait(false);
                if (this.variants1 == null)
                    this.variants1 = Comp.GetCache(typeof(Uniconta.DataModel.InvVariant1)) ?? await Comp.LoadCache(typeof(Uniconta.DataModel.InvVariant1), api).ConfigureAwait(false);
                if (this.variants2 == null)
                    this.variants2 = Comp.GetCache(typeof(Uniconta.DataModel.InvVariant2)) ?? await Comp.LoadCache(typeof(Uniconta.DataModel.InvVariant2), api).ConfigureAwait(false);
            }
            if (Comp.ProjectTask)
                ProjectCache = Comp.GetCache(typeof(Uniconta.DataModel.Project)) ?? await Comp.LoadCache(typeof(Uniconta.DataModel.Project), api).ConfigureAwait(false);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var selectedItem = dgCreditorOrderLineGrid.SelectedItem as CreditorOrderLineClient;
                if (selectedItem != null)
                {
                    if (selectedItem.Variant1Source == null)
                        setVariant(selectedItem, false);
                    if (selectedItem.Variant2Source == null)
                        setVariant(selectedItem, true);
                }
            }));
            if (Comp.CreditorPrice)
                PriceLookup = new Uniconta.API.DebtorCreditor.FindPrices(Order, api);
            else if (Order._Currency != 0 && Order._Currency != Comp._CurrencyId)
                exchangeRate = await api.session.ExchangeRate(Comp._CurrencyId, (Currencies)Order._Currency,
                    BasePage.GetSystemDefaultDate(), Comp).ConfigureAwait(false);
        }

        async void loadExchange()
        {
            exchangeRate = await api.session.ExchangeRate((Currencies)CompCurrency, (Currencies)OrderCurrency,
                BasePage.GetSystemDefaultDate(), api.CompanyEntity).ConfigureAwait(false);
        }

        public void setDimensions()
        {
            var c = api.CompanyEntity;
            lbldim1.Text = c._Dim1;
            lbldim2.Text = c._Dim2;
            lbldim3.Text = c._Dim3;
            lbldim4.Text = c._Dim4;
            lbldim5.Text = c._Dim5;

            int noofDimensions = c.NumberOfDimensions;
            if (noofDimensions < 5)
                lbldim5.Visibility = ledim5.Visibility = Visibility.Collapsed;
            if (noofDimensions < 4)
                lbldim4.Visibility = ledim4.Visibility = Visibility.Collapsed;
            if (noofDimensions < 3)
                lbldim3.Visibility = ledim3.Visibility = Visibility.Collapsed;
            if (noofDimensions < 2)
                lbldim2.Visibility = ledim2.Visibility = Visibility.Collapsed;
            if (noofDimensions < 1)
                lbldim1.Visibility = ledim1.Visibility = Visibility.Collapsed;
        }

        private void btnAccount_Click(object sender, RoutedEventArgs e)
        {
            object[] param = new object[2];
            param[0] = api;
            param[1] = null;
            AddDockItem(TabControls.CreditorAccountPage2, param, Uniconta.ClientTools.Localization.lookup("Creditorsaccount"), ";component/Assets/img/Add_16x16.png");
        }

        List<TableAddOnData> documents;
        FileBrowseControl fileBrowser;
        ItemBase attachDocMenu;
        void AttachDocuments()
        {
            if (fileBrowser == null)
                fileBrowser = new FileBrowseControl();
            else
                fileBrowser.SelectedFileInfos = null;
            fileBrowser.IsMultiSelect = true;
            fileBrowser.BrowseFile();
            var fileList = fileBrowser.SelectedFileInfos;
            if (fileList != null)
            {
                if (documents == null)
                    documents = new List<TableAddOnData>();
                foreach (var file in fileList)
                {
                    documents.Add(new TableAddOnData
                    {
                        _Text = System.IO.Path.GetFileNameWithoutExtension(file.FileName),
                        _DocumentType = DocumentConvert.GetDocumentType(file.FileExtension),
                        _Data = file.FileBytes
                    });
                }
            }

            if (documents?.Count > 0)
            {
                var rb = (RibbonBase)localMenu.DataContext;
                attachDocMenu = UtilDisplay.GetMenuCommandByName(rb, "AttachDoc");
                attachDocMenu.Caption = string.Format("{0} ({1})", string.Format(Uniconta.ClientTools.Localization.lookup("AttachOBJ"), Uniconta.ClientTools.Localization.lookup("Documents")), documents.Count);
            }
        }

        CorasauGridLookupEditorClient prevTask;
        private void Task_GotFocus(object sender, RoutedEventArgs e)
        {
            var selectedItem = dgCreditorOrderLineGrid.SelectedItem as CreditorOrderLineClient;
            if (selectedItem?._Project != null)
            {
                var selected = (ProjectClient)ProjectCache.Get(selectedItem._Project);
                setTask(selected, selectedItem);
                if (prevTask != null)
                    prevTask.isValidate = false;
                var editor = (CorasauGridLookupEditorClient)sender;
                prevTask = editor;
                editor.isValidate = true;
            }
        }

        async void setTask(ProjectClient project, CreditorOrderLineClient rec)
        {
            if (api.CompanyEntity.ProjectTask)
            {
                if (project != null)
                    rec.taskSource = project.Tasks ?? await project.LoadTasks(api);
                else
                {
                    rec.taskSource = null;
                    rec.Task = null;
                }
                rec.NotifyPropertyChanged("TaskSource");
            }
        }
    }
}
