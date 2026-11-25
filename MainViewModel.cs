using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;

namespace BudgetSuite
{
    public class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
    {
        private readonly Action<object?> _execute = execute;
        private readonly Predicate<object?>? _canExecute = canExecute;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private static readonly string SaveFilePath = GetSaveFilePath();

        private static string GetSaveFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private bool _isLoading = false;

        #region 네비게이션 탭 인덱스 (사이드바 연동)
        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { _selectedTabIndex = value; OnPropertyChanged(); }
        }
        public ICommand SelectTabCommand { get; }
        #endregion

        #region 클라우드 실시간 동기화 프로퍼티
        private string _cloudSyncStatusText = "💾 로컬 저장 모드";
        public string CloudSyncStatusText
        {
            get => _cloudSyncStatusText;
            set { _cloudSyncStatusText = value; OnPropertyChanged(); }
        }

        private bool _isSyncing = false;
        public bool IsSyncing
        {
            get => _isSyncing;
            set { _isSyncing = value; OnPropertyChanged(); }
        }

        private bool _isCloudConnected = false;
        public bool IsCloudConnected
        {
            get => _isCloudConnected;
            set { _isCloudConnected = value; OnPropertyChanged(); }
        }
        public ICommand RefreshFromCloudCommand { get; }
        #endregion

        #region Defender 프로퍼티 (일일 예산 방어)
        private decimal _currentBank = 0m;
        private decimal _targetBalance = 0m;
        private decimal _holdAmount = 0m;
        private decimal _naverPoints = 0m;
        private decimal _kakaoPayMoney = 0m;
        private bool _includeUnpaidFixed = false; // 미출금 고정지출도 통장 가용액에서 보호 차감 여부

        public decimal CurrentBank
        {
            get => _currentBank;
            set { _currentBank = value; OnDefenderChanged(); }
        }

        public decimal TargetBalance
        {
            get => _targetBalance;
            set { _targetBalance = value; OnDefenderChanged(); }
        }

        /// <summary>
        /// 해외 사이트 카드 등록 확인용 가승인/홀드금
        /// </summary>
        public decimal HoldAmount
        {
            get => _holdAmount;
            set { _holdAmount = value; OnDefenderChanged(); }
        }

        public bool IncludeUnpaidFixed
        {
            get => _includeUnpaidFixed;
            set { _includeUnpaidFixed = value; OnDefenderChanged(); }
        }

        public decimal NaverPoints
        {
            get => _naverPoints;
            set { _naverPoints = value; OnDefenderChanged(); }
        }

        public decimal KakaoPayMoney
        {
            get => _kakaoPayMoney;
            set { _kakaoPayMoney = value; OnDefenderChanged(); }
        }
        #endregion

        #region Planner 프로퍼티 (동적 수당 & 투자 & 고정지출)
        // 1. 수당/수입 계획 동적 목록
        public ObservableCollection<PlanItem> IncomePlans { get; } = [];

        // 2. 투자/저축 계획 동적 목록
        public ObservableCollection<PlanItem> InvestmentPlans { get; } = [];

        // 3. 고정지출 체크리스트 동적 목록
        public ObservableCollection<FixedExpenseItem> FixedExpenses { get; } = [];
        #endregion

        #region 거래 등록 폼 (지출, 수입 및 이체)
        private string _transactionMode = "지출"; // "지출", "수입", "이체"
        public string TransactionMode
        {
            get => _transactionMode;
            set
            {
                if (_transactionMode == value) return;
                _transactionMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsExpenseMode));
                OnPropertyChanged(nameof(IsIncomeMode));
                OnPropertyChanged(nameof(IsTransferMode));
                OnPropertyChanged(nameof(FormTitleText));
                OnPropertyChanged(nameof(AddButtonText));
                OnPropertyChanged(nameof(DescLabelText));
                OnPropertyChanged(nameof(DescPlaceholderText));
                OnPropertyChanged(nameof(MethodLabelText));
            }
        }

        public bool IsExpenseMode
        {
            get => TransactionMode == "지출";
            set => TransactionMode = value ? "지출" : "수입";
        }

        public bool IsIncomeMode => TransactionMode == "수입";
        public bool IsTransferMode => TransactionMode == "이체";

        public string FormTitleText => TransactionMode switch
        {
            "수입" => "📥 오늘 수입 빠른 등록",
            "이체" => "🔀 계좌 이체 / 분배 등록",
            _ => "✍️ 오늘 지출 빠른 등록"
        };

        public string AddButtonText => TransactionMode switch
        {
            "수입" => "+ 수입 추가",
            "이체" => "+ 이체 추가",
            _ => "+ 지출 추가"
        };

        public string DescLabelText => TransactionMode switch
        {
            "수입" => "수입 내역 (항목명)",
            "이체" => "이체 내역 (항목명)",
            _ => "지출 내역 (항목명)"
        };

        public string DescPlaceholderText => TransactionMode switch
        {
            "수입" => "예: 알바비, 중고거래 당근 정산, 용돈, 캐시백 등",
            "이체" => "예: 구직촉진수당 분배, 투자금 이동 등 (비워두면 자동 입력)",
            _ => "예: 점심 식사, 커피, 편의점 등"
        };

        public string MethodLabelText => TransactionMode switch
        {
            "수입" => "입금 계좌/수단",
            "이체" => "이체 대상 계좌",
            _ => "결제 수단"
        };

        private List<ExpenseItem> _allExpenses = [];
        public ObservableCollection<ExpenseItem> TodayExpenses { get; } = [];
        public ObservableCollection<ExpenseItem> MonthlyHistoryExpenses { get; } = [];

        private ExpenseItem? _selectedTodayExpense;
        public ExpenseItem? SelectedTodayExpense
        {
            get => _selectedTodayExpense;
            set { _selectedTodayExpense = value; OnPropertyChanged(); }
        }

        private ExpenseItem? _selectedHistoryExpense;
        public ExpenseItem? SelectedHistoryExpense
        {
            get => _selectedHistoryExpense;
            set { _selectedHistoryExpense = value; OnPropertyChanged(); }
        }

        private string _newDesc = string.Empty;
        public string NewDesc
        {
            get => _newDesc;
            set { _newDesc = value; OnPropertyChanged(); }
        }

        private decimal _newAmount;
        public decimal NewAmount
        {
            get => _newAmount;
            set { _newAmount = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> PaymentOptions { get; } =
        [
            "현금/체크",
            "네이버페이 포인트",
            "카카오페이머니"
        ];

        private string _newPaymentMethod = "현금/체크";
        public string NewPaymentMethod
        {
            get => _newPaymentMethod;
            set
            {
                _newPaymentMethod = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCashSelected));
                OnPropertyChanged(nameof(IsNaverSelected));
                OnPropertyChanged(nameof(IsKakaoSelected));
            }
        }

        public bool IsCashSelected => string.IsNullOrEmpty(NewPaymentMethod) || NewPaymentMethod == "현금/체크" || NewPaymentMethod.Contains("현금");
        public bool IsNaverSelected => NewPaymentMethod != null && NewPaymentMethod.Contains("네이버");
        public bool IsKakaoSelected => NewPaymentMethod != null && NewPaymentMethod.Contains("카카오");

        private string _transferTarget = "카카오페이머니";
        public string TransferTarget
        {
            get => _transferTarget;
            set
            {
                _transferTarget = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTransferKakaoSelected));
                OnPropertyChanged(nameof(IsTransferKiwoomSelected));
            }
        }

        public bool IsTransferKakaoSelected => TransferTarget == "카카오페이머니";
        public bool IsTransferKiwoomSelected => TransferTarget == "키움증권계좌";
        #endregion

        #region 연산 프로퍼티 (Defender)
        public string TodayDateString => DateTime.Today.ToString("yyyy-MM-dd");
        public int DaysInMonth => DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month);
        public int RemainingDays
        {
            get
            {
                int rem = DaysInMonth - DateTime.Today.Day + 1;
                return rem <= 0 ? 1 : rem;
            }
        }

        public string DateInfoText => $"{TodayDateString} (남은 일수: {RemainingDays}일)";

        // 오늘 현금 지출 & 수입
        public decimal SpentTodayCash => TodayExpenses.Where(x => (x.ItemType == "지출" || string.IsNullOrEmpty(x.ItemType)) && (x.PaymentMethod == "현금/체크" || x.PaymentMethod.Contains("현금"))).Sum(x => x.Amount);
        public decimal IncomeTodayCash => TodayExpenses.Where(x => x.ItemType == "수입" && (x.PaymentMethod == "현금/체크" || x.PaymentMethod.Contains("현금"))).Sum(x => x.Amount);

        private static bool IsPointInflow(ExpenseItem x) =>
            (x.ItemType == "수입" && !x.PaymentMethod.Contains("현금")) ||
            (x.ItemType == "이체" && x.PaymentMethod.Contains("카카오"));

        // 오늘 포인트 지출, 유입 및 이체 총액
        public decimal SpentTodayPoints => TodayExpenses.Where(x => (x.ItemType == "지출" || string.IsNullOrEmpty(x.ItemType)) && !x.PaymentMethod.Contains("현금")).Sum(x => x.Amount);
        public decimal IncomeTodayPoints => TodayExpenses.Where(IsPointInflow).Sum(x => x.Amount);
        public decimal TransferTodayTotal => TodayExpenses.Where(x => x.ItemType == "이체").Sum(x => x.Amount);

        public decimal TotalShieldPoints => NaverPoints + KakaoPayMoney;

        /// <summary>
        /// 오늘 기준 가용 통장 잔고 (실시간 잔고 + 오늘 현금 지출, 오늘 수입은 즉시 반영)
        /// </summary>
        public decimal StartOfDayBank => CurrentBank + SpentTodayCash;

        public decimal TotalSpendable => StartOfDayBank - TargetBalance;

        public decimal DailyBaseLimit => TotalSpendable > 0 ? (TotalSpendable / RemainingDays) : 0m;
        public decimal DailyRemainingRaw => DailyBaseLimit - SpentTodayCash;

        public decimal DailyAvailableRounded
        {
            get
            {
                if (TotalSpendable <= 0) return 0m;
                if (DailyRemainingRaw <= 0) return 0m;
                return Math.Floor(DailyRemainingRaw / 1000m) * 1000m;
            }
        }

        public string DailyStatusMessage
        {
            get
            {
                if (TotalSpendable <= 0) return "⚠️ 통장 가용 잔액 부족 (지출 잠금 요망!)";
                if (DailyRemainingRaw <= 0) return $"오늘치 한도 초과 소진 (기본 한도: {DailyBaseLimit:N0}원)";
                return $"절사 전 잔여: {DailyRemainingRaw:N0}원 | 1일 기본 한도: {DailyBaseLimit:N0}원";
            }
        }
        #endregion

        #region 연산 프로퍼티 (Planner)
        public decimal TotalIncome => IncomePlans.Sum(x => x.Amount);
        public decimal TotalInvestment => InvestmentPlans.Sum(x => x.Amount);
        public decimal AccountBalance => TotalIncome - TotalInvestment;
        public decimal TotalFixedExpenses => FixedExpenses.Sum(x => x.Amount);
        public decimal RemainingFixedExpenses => FixedExpenses.Where(x => !x.IsPaid).Sum(x => x.Amount);
        public decimal PaidFixedExpenses => FixedExpenses.Where(x => x.IsPaid).Sum(x => x.Amount);
        public decimal NetRemainingFunds => AccountBalance - TotalFixedExpenses;
        public decimal FinalEmergencyFund => NetRemainingFunds;
        #endregion

        #region 연산 프로퍼티 (Monthly Analytics)
        public decimal MonthTotalSpent
        {
            get
            {
                string curMonth = DateTime.Today.ToString("yyyy-MM");
                return _allExpenses.Where(x => x.Date.StartsWith(curMonth) && (x.ItemType == "지출" || string.IsNullOrEmpty(x.ItemType))).Sum(x => x.Amount);
            }
        }

        public decimal MonthTotalIncome
        {
            get
            {
                string curMonth = DateTime.Today.ToString("yyyy-MM");
                return _allExpenses.Where(x => x.Date.StartsWith(curMonth) && x.ItemType == "수입").Sum(x => x.Amount);
            }
        }

        public decimal MonthCashIncome
        {
            get
            {
                string curMonth = DateTime.Today.ToString("yyyy-MM");
                return _allExpenses.Where(x => x.Date.StartsWith(curMonth) && x.ItemType == "수입" && (x.PaymentMethod == "현금/체크" || x.PaymentMethod.Contains("현금"))).Sum(x => x.Amount);
            }
        }

        public decimal MonthPointIncome
        {
            get
            {
                string curMonth = DateTime.Today.ToString("yyyy-MM");
                return _allExpenses.Where(x => x.Date.StartsWith(curMonth) && IsPointInflow(x)).Sum(x => x.Amount);
            }
        }

        public decimal MonthTransferTotal
        {
            get
            {
                string curMonth = DateTime.Today.ToString("yyyy-MM");
                return _allExpenses.Where(x => x.Date.StartsWith(curMonth) && x.ItemType == "이체").Sum(x => x.Amount);
            }
        }

        public decimal MonthCashSpent
        {
            get
            {
                string curMonth = DateTime.Today.ToString("yyyy-MM");
                return _allExpenses.Where(x => x.Date.StartsWith(curMonth) && (x.ItemType == "지출" || string.IsNullOrEmpty(x.ItemType)) && x.PaymentMethod == "현금/체크").Sum(x => x.Amount);
            }
        }

        public decimal MonthPointSpent
        {
            get
            {
                string curMonth = DateTime.Today.ToString("yyyy-MM");
                return _allExpenses.Where(x => x.Date.StartsWith(curMonth) && (x.ItemType == "지출" || string.IsNullOrEmpty(x.ItemType)) && x.PaymentMethod != "현금/체크").Sum(x => x.Amount);
            }
        }

        public int MonthRecordedDays
        {
            get
            {
                string curMonth = DateTime.Today.ToString("yyyy-MM");
                int days = _allExpenses.Where(x => x.Date.StartsWith(curMonth)).Select(x => x.Date).Distinct().Count();
                return days <= 0 ? 1 : days;
            }
        }

        public decimal MonthDailyAvgCash => MonthCashSpent / MonthRecordedDays;
        #endregion

        #region 커맨드
        // 거래(지출/수입/이체) 등록 및 모드 전환
        public ICommand AddTransactionCommand { get; }
        public ICommand AddExpenseCommand => AddTransactionCommand; // 하위 호환
        public ICommand SetTransactionModeCommand { get; }
        public ICommand SelectPaymentMethodCommand { get; }
        public ICommand SelectTransferTargetCommand { get; }

        public ICommand DeleteTodayExpenseCommand { get; }
        public ICommand DeleteHistoryExpenseCommand { get; }

        // 수당 & 투자 계획 커맨드
        public ICommand AddIncomePlanCommand { get; }
        public ICommand DeleteIncomePlanCommand { get; }
        public ICommand AddInvestmentPlanCommand { get; }
        public ICommand DeleteInvestmentPlanCommand { get; }

        // 고정지출 커맨드
        public ICommand AddFixedExpenseCommand { get; }
        public ICommand DeleteFixedExpenseCommand { get; }
        public ICommand ResetFixedExpensesCommand { get; }

        // 스마트 연동 커맨드
        public ICommand SyncTargetBalanceCommand { get; }
        public ICommand SyncKakaoMoneyCommand { get; }
        public ICommand SyncAllToDefenderCommand { get; }
        #endregion

        public MainViewModel()
        {
            SelectTabCommand = new RelayCommand(param =>
            {
                if (param != null && int.TryParse(param.ToString(), out int idx))
                {
                    SelectedTabIndex = idx;
                }
            });

            // 거래 등록 & 모드 전환
            SetTransactionModeCommand = new RelayCommand(param =>
            {
                string? mode = param?.ToString();
                if (mode == "수입" || mode == "이체")
                    TransactionMode = mode;
                else
                    TransactionMode = "지출";
            });

            SelectPaymentMethodCommand = new RelayCommand(param =>
            {
                if (param is string method)
                    NewPaymentMethod = method;
            });

            SelectTransferTargetCommand = new RelayCommand(param =>
            {
                if (param is string target)
                    TransferTarget = target;
            });

            AddTransactionCommand = new RelayCommand(_ => AddTransaction());
            DeleteTodayExpenseCommand = new RelayCommand(param => DeleteExpense((param as ExpenseItem) ?? SelectedTodayExpense));
            DeleteHistoryExpenseCommand = new RelayCommand(param => DeleteExpense((param as ExpenseItem) ?? SelectedHistoryExpense));

            // 수당 및 투자 계획 추가/삭제
            AddIncomePlanCommand = new RelayCommand(_ => AddIncomePlan());
            DeleteIncomePlanCommand = new RelayCommand(param => DeleteIncomePlan(param as PlanItem));
            AddInvestmentPlanCommand = new RelayCommand(_ => AddInvestmentPlan());
            DeleteInvestmentPlanCommand = new RelayCommand(param => DeleteInvestmentPlan(param as PlanItem));

            // 고정지출 추가/삭제
            AddFixedExpenseCommand = new RelayCommand(_ => AddFixedExpense());
            DeleteFixedExpenseCommand = new RelayCommand(param => DeleteFixedExpense(param as FixedExpenseItem));
            ResetFixedExpensesCommand = new RelayCommand(_ => ResetFixedExpenses());

            // 스마트 동기화 & 클라우드 수동 새로고침
            SyncTargetBalanceCommand = new RelayCommand(_ => SyncTargetBalance());
            SyncKakaoMoneyCommand = new RelayCommand(_ => SyncKakaoMoney());
            SyncAllToDefenderCommand = new RelayCommand(_ => SyncAllToDefender());
            RefreshFromCloudCommand = new RelayCommand(async _ => await RefreshFromCloudAsync(true));

            // 컬렉션 변경 리스너
            IncomePlans.CollectionChanged += IncomePlans_CollectionChanged;
            InvestmentPlans.CollectionChanged += InvestmentPlans_CollectionChanged;
            FixedExpenses.CollectionChanged += FixedExpenses_CollectionChanged;

            LoadData();
            StartAutoSyncTimer();
        }

        #region 실시간 백그라운드 동기화 (모바일 ↔ PC)
        private DateTime _lastSyncTime = DateTime.MinValue;
        private string _lastKnownMonth = DateTime.Today.ToString("yyyy-MM");
        private System.Windows.Threading.DispatcherTimer? _autoSyncTimer;
        private int _cloudCheckTick = 0;
        private bool _isCheckingCloud = false;

        private void StartAutoSyncTimer()
        {
            _autoSyncTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _autoSyncTimer.Tick += (s, e) => CheckForRemoteUpdates();
            _autoSyncTimer.Start();
        }

        public async void CheckForRemoteUpdates()
        {
            if (_isLoading || _isSyncing) return;

            try
            {
                // 0. 프로그램 실행 중 자정이 지나 새 달이 된 경우 감지 & 고정지출 체크 자동 해제
                string currentMonth = DateTime.Today.ToString("yyyy-MM");
                if (_lastKnownMonth != currentMonth)
                {
                    _lastKnownMonth = currentMonth;
                    foreach (var fe in FixedExpenses)
                    {
                        fe.IsPaid = false;
                    }
                    _paidFixedSnapshot.Clear();
                    RefreshExpenseViews();
                    OnPlannerChanged();
                    SaveData();
                    return;
                }

                // 1. 로컬 파일 수정 감지
                if (File.Exists(SaveFilePath))
                {
                    DateTime fileTime = File.GetLastWriteTimeUtc(SaveFilePath);
                    if (fileTime > _lastSyncTime)
                    {
                        LoadData();
                        return;
                    }
                }

                // 2. 클라우드 DB 주기적 변경 감지 (약 3초마다 경량 SELECT updated_at 쿼리 확인)
                if (DatabaseService.IsConfigured)
                {
                    _cloudCheckTick++;
                    if (_cloudCheckTick >= 2)
                    {
                        _cloudCheckTick = 0;
                        if (!_isCheckingCloud)
                        {
                            _isCheckingCloud = true;
                            try
                            {
                                var remoteTs = await Task.Run(() => DatabaseService.GetLatestDbTimestamp());
                                if (remoteTs.HasValue && remoteTs.Value > DatabaseService.LastDbUpdatedTime)
                                {
                                    await RefreshFromCloudAsync(false);
                                }
                            }
                            catch { }
                            finally
                            {
                                _isCheckingCloud = false;
                            }
                        }
                    }
                }
            }
            catch { }
        }
        #endregion

        #region 수당 및 투자 계획 관리
        private void AddIncomePlan()
        {
            IncomePlans.Add(new PlanItem { Name = "새 수당/수입", Amount = 0m });
            OnPlannerChanged();
        }

        private void DeleteIncomePlan(PlanItem? item)
        {
            if (item != null && IncomePlans.Contains(item))
            {
                item.PropertyChanged -= PlanItem_PropertyChanged;
                IncomePlans.Remove(item);
                OnPlannerChanged();
            }
        }

        private void AddInvestmentPlan()
        {
            InvestmentPlans.Add(new PlanItem { Name = "새 투자/저축", Amount = 0m });
            OnPlannerChanged();
        }

        private void DeleteInvestmentPlan(PlanItem? item)
        {
            if (item != null && InvestmentPlans.Contains(item))
            {
                item.PropertyChanged -= PlanItem_PropertyChanged;
                InvestmentPlans.Remove(item);
                OnPlannerChanged();
            }
        }

        private void IncomePlans_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (PlanItem item in e.NewItems)
                    item.PropertyChanged += PlanItem_PropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (PlanItem item in e.OldItems)
                    item.PropertyChanged -= PlanItem_PropertyChanged;
            }
            OnPlannerChanged();
        }

        private void InvestmentPlans_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (PlanItem item in e.NewItems)
                    item.PropertyChanged += PlanItem_PropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (PlanItem item in e.OldItems)
                    item.PropertyChanged -= PlanItem_PropertyChanged;
            }
            OnPlannerChanged();
        }

        private void PlanItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPlannerChanged();
        }
        #endregion

        #region 스마트 동기화 메서드
        private void SyncTargetBalance()
        {
            TargetBalance = Math.Max(0, FinalEmergencyFund);
            MessageBox.Show($"월말 최종 비상금 {FinalEmergencyFund:N0}원이 월말 목표 잔고로 반영되었습니다.", "동기화 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SyncKakaoMoney()
        {
            // 투자 계획 중 카카오페이/카카오 관련 항목 탐색
            var kakaoPlan = InvestmentPlans.FirstOrDefault(x => x.Name.Contains("카카오"));
            if (kakaoPlan != null)
            {
                KakaoPayMoney = kakaoPlan.Amount;
                MessageBox.Show($"투자 계획의 '{kakaoPlan.Name}' ({kakaoPlan.Amount:N0}원)이 카카오페이머니 방패 잔액으로 반영되었습니다.", "동기화 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("투자 계획에 '카카오' 항목이 없습니다. 필요 시 투자 계획에 '카카오페이머니'를 추가해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SyncAllToDefender()
        {
            TargetBalance = Math.Max(0, FinalEmergencyFund);
            var kakaoPlan = InvestmentPlans.FirstOrDefault(x => x.Name.Contains("카카오"));
            if (kakaoPlan != null) KakaoPayMoney = kakaoPlan.Amount;
            MessageBox.Show($"플래너의 최종 비상금({FinalEmergencyFund:N0}원)이 월말 목표 잔고로 실시간 자동 연동되고 있습니다!", "실시간 자동 연동", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region 거래 (지출/수입/이체) 등록 및 삭제
        private void AddTransaction()
        {
            if (!IsTransferMode && string.IsNullOrWhiteSpace(NewDesc))
            {
                string msg = IsExpenseMode ? "지출 내용을 입력하세요." : "수입 내용을 입력하세요.";
                MessageBox.Show(msg, "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NewAmount <= 0)
            {
                MessageBox.Show("금액을 0원보다 크게 입력하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string targetOrMethod = IsTransferMode
                ? (string.IsNullOrEmpty(TransferTarget) ? "카카오페이머니" : TransferTarget)
                : (string.IsNullOrEmpty(NewPaymentMethod) ? "현금/체크" : NewPaymentMethod);
            string type = TransactionMode;
            string description = string.IsNullOrWhiteSpace(NewDesc) ? $"{targetOrMethod} 이체" : NewDesc.Trim();

            // 지출 또는 이체 시 잔액 부족 확인
            if (IsExpenseMode || IsTransferMode)
            {
                decimal available = IsTransferMode ? CurrentBank : GetBalanceFor(targetOrMethod);
                string sourceName = IsTransferMode ? "통장 잔고" : $"{targetOrMethod} 잔액";
                if (NewAmount > available)
                {
                    if (MessageBox.Show($"{sourceName}({available:N0}원)보다 큰 금액입니다.\n그래도 등록하시겠습니까? (잔액이 마이너스가 됩니다)",
                            "잔액 부족", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }
            }

            var newItem = new ExpenseItem
            {
                Date = DateTime.Today.ToString("yyyy-MM-dd"),
                Time = DateTime.Now.ToString("HH:mm"),
                ItemType = type,
                Description = description,
                Amount = NewAmount,
                PaymentMethod = targetOrMethod
            };

            _allExpenses.Add(newItem);
            TodayExpenses.Insert(0, newItem);
            MonthlyHistoryExpenses.Insert(0, newItem);

            // 실시간 잔고 변경 (지출: 차감, 수입: 가산, 이체: 통장 차감 + 대상 계좌 연동)
            if (IsTransferMode)
                ApplyTransferBalance(targetOrMethod, newItem.Amount, isRevert: false);
            else if (IsExpenseMode)
                ApplyBalanceChange(targetOrMethod, -newItem.Amount);
            else
                ApplyBalanceChange(targetOrMethod, +newItem.Amount);

            NewDesc = string.Empty;
            NewAmount = 0;

            OnDefenderChanged();
            OnAnalyticsChanged();
            SaveData();
        }

        private void DeleteExpense(ExpenseItem? item)
        {
            if (item == null)
            {
                MessageBox.Show("삭제할 지출/수입/이체 내역을 표(목록)에서 먼저 마우스로 클릭하여 선택한 후 [선택 항목 삭제] 버튼을 눌러주세요.", "항목 미선택", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string confirmMsg;
            if (item.ItemType == "이체")
            {
                string extra = item.PaymentMethod.Contains("카카오")
                    ? $"되고, 카카오페이머니에서 {item.Amount:N0}원이 차감"
                    : "";
                confirmMsg = $"'{item.Description}' (이체 {item.Amount:N0}원) 항목을 삭제하시겠습니까?\n삭제 시 통장 잔고에 {item.Amount:N0}원이 다시 복구{extra}됩니다.";
            }
            else
            {
                string typeStr = item.ItemType == "수입" ? "수입" : "지출";
                string effectStr = item.ItemType == "수입" ? "차감" : "복구";
                confirmMsg = $"'{item.Description}' ({typeStr} {item.Amount:N0}원) 항목을 삭제하시겠습니까?\n삭제 시 {item.PaymentMethod} 잔고에서 {item.Amount:N0}원이 다시 {effectStr}됩니다.";
            }

            if (MessageBox.Show(confirmMsg, "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            // 참조 또는 동일 속성 매칭 삭제 (데이터 재동기화 후 인스턴스 불일치 완벽 방지)
            var matchAll = _allExpenses.FirstOrDefault(x => ReferenceEquals(x, item) || 
                (x.Date == item.Date && x.Time == item.Time && x.ItemType == item.ItemType && x.Description == item.Description && x.Amount == item.Amount));
            if (matchAll != null) _allExpenses.Remove(matchAll);

            var matchToday = TodayExpenses.FirstOrDefault(x => ReferenceEquals(x, item) || 
                (x.Date == item.Date && x.Time == item.Time && x.ItemType == item.ItemType && x.Description == item.Description && x.Amount == item.Amount));
            if (matchToday != null) TodayExpenses.Remove(matchToday);

            var matchHist = MonthlyHistoryExpenses.FirstOrDefault(x => ReferenceEquals(x, item) || 
                (x.Date == item.Date && x.Time == item.Time && x.ItemType == item.ItemType && x.Description == item.Description && x.Amount == item.Amount));
            if (matchHist != null) MonthlyHistoryExpenses.Remove(matchHist);

            SelectedTodayExpense = null;
            SelectedHistoryExpense = null;

            // 실시간 잔고 복구 (이체 삭제: 양쪽 복구, 지출 삭제: 가산, 수입 삭제: 차감)
            if (item.ItemType == "이체")
                ApplyTransferBalance(item.PaymentMethod, item.Amount, isRevert: true);
            else if (item.ItemType == "수입")
                ApplyBalanceChange(item.PaymentMethod, -item.Amount);
            else
                ApplyBalanceChange(item.PaymentMethod, +item.Amount);

            OnDefenderChanged();
            OnAnalyticsChanged();
            SaveData();
        }

        private decimal GetBalanceFor(string method)
        {
            if (method.Contains("네이버")) return NaverPoints;
            if (method.Contains("카카오")) return KakaoPayMoney;
            return CurrentBank;
        }

        private void ApplyBalanceChange(string method, decimal delta)
        {
            if (method.Contains("네이버"))
            {
                NaverPoints += delta;
            }
            else if (method.Contains("카카오"))
            {
                KakaoPayMoney += delta;
            }
            else
            {
                CurrentBank += delta;
            }
        }

        /// <summary>
        /// 통장에서 이체 대상 계좌(카카오페이머니, 키움증권계좌 등)로의 자금 이동 반영 및 삭제 시 복구
        /// </summary>
        private void ApplyTransferBalance(string target, decimal amount, bool isRevert)
        {
            decimal sign = isRevert ? 1m : -1m;
            CurrentBank += sign * amount;

            if (target.Contains("카카오"))
            {
                KakaoPayMoney -= sign * amount;
            }
        }
        #endregion

        #region 고정지출 관리
        private void AddFixedExpense()
        {
            string baseName = "새 고정지출";
            string name = baseName;
            int counter = 1;
            while (FixedExpenses.Any(x => x.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"{baseName} ({++counter})";
            }
            FixedExpenses.Add(new FixedExpenseItem { Name = name, Amount = 0m, IsPaid = false });
            OnPlannerChanged();
        }

        private void DeleteFixedExpense(FixedExpenseItem? item)
        {
            if (item != null && FixedExpenses.Contains(item))
            {
                item.PropertyChanged -= Item_PropertyChanged;
                if (item.IsPaid && _paidFixedSnapshot.TryGetValue(item, out var paid))
                {
                    RevertFixedExpensePayment(paid);
                }
                _paidFixedSnapshot.Remove(item);
                FixedExpenses.Remove(item);
                OnPlannerChanged();
            }
        }

        private void ResetFixedExpenses(bool showMessage = true)
        {
            if (showMessage && MessageBox.Show("이번 달 모든 고정지출의 납부 완료 체크를 해제(미납 상태로 리셋)하시겠습니까?\n체크 시 차감되었던 금액 계산도 초기화됩니다.", "체크 전체 초기화", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (var fe in FixedExpenses)
            {
                fe.IsPaid = false;
            }
            _paidFixedSnapshot.Clear();
            RefreshExpenseViews();
            OnPlannerChanged();
            SaveData();

            if (showMessage)
            {
                MessageBox.Show("이번 달 고정지출 체크가 모두 정상적으로 해제되었습니다!", "초기화 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void FixedExpenses_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (FixedExpenseItem item in e.NewItems)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                    if (item.IsPaid) _paidFixedSnapshot[item] = (item.Amount, 0m);
                }
            }
            if (e.OldItems != null)
            {
                foreach (FixedExpenseItem item in e.OldItems)
                    item.PropertyChanged -= Item_PropertyChanged;
            }
            OnPlannerChanged();
        }

        private readonly Dictionary<FixedExpenseItem, (decimal Bank, decimal NaverPoint)> _paidFixedSnapshot = [];

        private static bool IsNaverPointPriorityFixed(FixedExpenseItem item) =>
            !string.IsNullOrWhiteSpace(item.Name) &&
            item.Name.Contains("네이버", StringComparison.OrdinalIgnoreCase);

        private void ApplyFixedExpensePayment(FixedExpenseItem item)
        {
            decimal amount = item.Amount;
            decimal usedNaver = 0m;
            decimal usedBank = amount;

            if (IsNaverPointPriorityFixed(item) && amount > 0m)
            {
                usedNaver = Math.Min(Math.Max(0m, _naverPoints), amount);
                usedBank = amount - usedNaver;
            }

            _naverPoints -= usedNaver;
            _currentBank -= usedBank;
            _paidFixedSnapshot[item] = (usedBank, usedNaver);
            OnDefenderChanged();
        }

        private void RevertFixedExpensePayment((decimal Bank, decimal NaverPoint) paid)
        {
            _currentBank += paid.Bank;
            _naverPoints += paid.NaverPoint;
            OnDefenderChanged();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_isLoading && sender is FixedExpenseItem item)
            {
                if (e.PropertyName == nameof(FixedExpenseItem.IsPaid))
                {
                    if (item.IsPaid)
                    {
                        ApplyFixedExpensePayment(item);
                    }
                    else if (_paidFixedSnapshot.TryGetValue(item, out var paid))
                    {
                        RevertFixedExpensePayment(paid);
                        _paidFixedSnapshot.Remove(item);
                    }
                }
                else if (e.PropertyName == nameof(FixedExpenseItem.Amount) && item.IsPaid
                         && _paidFixedSnapshot.TryGetValue(item, out var prev))
                {
                    decimal delta = item.Amount - (prev.Bank + prev.NaverPoint);
                    if (delta > 0m)
                    {
                        decimal addNaver = IsNaverPointPriorityFixed(item)
                            ? Math.Min(Math.Max(0m, _naverPoints), delta)
                            : 0m;
                        decimal addBank = delta - addNaver;

                        _naverPoints -= addNaver;
                        _currentBank -= addBank;
                        _paidFixedSnapshot[item] = (prev.Bank + addBank, prev.NaverPoint + addNaver);
                        OnDefenderChanged();
                    }
                    else if (delta < 0m)
                    {
                        decimal refund = -delta;
                        decimal refundBank = Math.Min(Math.Max(0m, prev.Bank), refund);
                        decimal refundNaver = Math.Min(Math.Max(0m, prev.NaverPoint), refund - refundBank);
                        refundBank += refund - refundBank - refundNaver;

                        _currentBank += refundBank;
                        _naverPoints += refundNaver;
                        _paidFixedSnapshot[item] = (prev.Bank - refundBank, prev.NaverPoint - refundNaver);
                        OnDefenderChanged();
                    }
                }
            }
            OnPlannerChanged();
        }
        #endregion

        #region 프로퍼티 변경 알림
        private void OnDefenderChanged()
        {
            OnPropertyChanged(nameof(CurrentBank));
            OnPropertyChanged(nameof(TargetBalance));
            OnPropertyChanged(nameof(HoldAmount));
            OnPropertyChanged(nameof(NaverPoints));
            OnPropertyChanged(nameof(KakaoPayMoney));
            OnPropertyChanged(nameof(SpentTodayCash));
            OnPropertyChanged(nameof(IncomeTodayCash));
            OnPropertyChanged(nameof(SpentTodayPoints));
            OnPropertyChanged(nameof(IncomeTodayPoints));
            OnPropertyChanged(nameof(TransferTodayTotal));
            OnPropertyChanged(nameof(TotalShieldPoints));
            OnPropertyChanged(nameof(StartOfDayBank));
            OnPropertyChanged(nameof(TotalSpendable));
            OnPropertyChanged(nameof(DailyBaseLimit));
            OnPropertyChanged(nameof(DailyRemainingRaw));
            OnPropertyChanged(nameof(DailyAvailableRounded));
            OnPropertyChanged(nameof(DailyStatusMessage));

            SaveData();
        }

        private void OnPlannerChanged()
        {
            OnPropertyChanged(nameof(TotalIncome));
            OnPropertyChanged(nameof(TotalInvestment));
            OnPropertyChanged(nameof(AccountBalance));
            OnPropertyChanged(nameof(TotalFixedExpenses));
            OnPropertyChanged(nameof(RemainingFixedExpenses));
            OnPropertyChanged(nameof(PaidFixedExpenses));
            OnPropertyChanged(nameof(NetRemainingFunds));
            OnPropertyChanged(nameof(FinalEmergencyFund));
            OnPropertyChanged(nameof(CurrentBank));

            // 방어기 실시간 한도 재계산 알림
            OnPropertyChanged(nameof(TargetBalance));
            OnPropertyChanged(nameof(TotalSpendable));
            OnPropertyChanged(nameof(DailyBaseLimit));
            OnPropertyChanged(nameof(DailyRemainingRaw));
            OnPropertyChanged(nameof(DailyAvailableRounded));
            OnPropertyChanged(nameof(DailyStatusMessage));

            SaveData();
        }

        private void OnAnalyticsChanged()
        {
            OnPropertyChanged(nameof(MonthTotalSpent));
            OnPropertyChanged(nameof(MonthTotalIncome));
            OnPropertyChanged(nameof(MonthCashSpent));
            OnPropertyChanged(nameof(MonthCashIncome));
            OnPropertyChanged(nameof(MonthPointSpent));
            OnPropertyChanged(nameof(MonthPointIncome));
            OnPropertyChanged(nameof(MonthTransferTotal));
            OnPropertyChanged(nameof(MonthRecordedDays));
            OnPropertyChanged(nameof(MonthDailyAvgCash));
        }
        #endregion

        #region 데이터 로드 및 저장 (마이그레이션 포함)
        private bool ApplyData(AppData loadedData)
        {
            _currentBank = loadedData.CurrentBank;
            _targetBalance = loadedData.TargetBalance;
            _holdAmount = loadedData.HoldAmount;
            _includeUnpaidFixed = loadedData.IncludeUnpaidFixed;
            _naverPoints = loadedData.NaverPoints;
            _kakaoPayMoney = loadedData.KakaoPayMoney;
            _allExpenses = loadedData.AllExpenseHistory ?? [];

            // 1. 수당/수입 계획 목록 로드 (구 버전 호환)
            IncomePlans.Clear();
            if (loadedData.IncomePlans != null && loadedData.IncomePlans.Count > 0)
            {
                foreach (var p in loadedData.IncomePlans)
                    IncomePlans.Add(p);
            }
            else
            {
                // 구 버전 필드에서 자동 생성
                if (loadedData.JobAllowance > 0 || loadedData.TrainingAllowance > 0)
                {
                    if (loadedData.JobAllowance > 0)
                        IncomePlans.Add(new PlanItem { Name = "월급 / 주 수입", Amount = loadedData.JobAllowance });
                    if (loadedData.TrainingAllowance > 0)
                        IncomePlans.Add(new PlanItem { Name = "보너스 / 부수입", Amount = loadedData.TrainingAllowance });
                }
                else
                {
                    AddDefaultIncomePlans();
                }
            }

            // 2. 투자/저축 계획 목록 로드 (구 버전 호환)
            InvestmentPlans.Clear();
            if (loadedData.InvestmentPlans != null && loadedData.InvestmentPlans.Count > 0)
            {
                foreach (var p in loadedData.InvestmentPlans)
                    InvestmentPlans.Add(p);
            }
            else
            {
                // 구 버전 필드에서 자동 생성
                if (loadedData.InvestKiwoom > 0 || loadedData.InvestKakao > 0)
                {
                    if (loadedData.InvestKiwoom > 0)
                        InvestmentPlans.Add(new PlanItem { Name = "저축 / 투자", Amount = loadedData.InvestKiwoom });
                    if (loadedData.InvestKakao > 0)
                        InvestmentPlans.Add(new PlanItem { Name = "카카오머니 (용돈)", Amount = loadedData.InvestKakao });
                }
                else
                {
                    AddDefaultInvestmentPlans();
                }
            }

            // 3. 고정지출 목록 로드 (새 달 자동 리셋 & 중복 방지)
            string currentMonth = DateTime.Today.ToString("yyyy-MM");
            bool isNewMonth = !string.IsNullOrEmpty(loadedData.LastActivateMonth) && loadedData.LastActivateMonth != currentMonth;

            // 추가 안전망: 월초(거래 0건)에 고정지출 전체가 이미 체크(IsPaid)되어 있다면 지난달 체크 잔재로 판별하여 자동 리셋
            bool hasCurrentMonthTxs = loadedData.AllExpenseHistory != null && loadedData.AllExpenseHistory.Any(x => x.Date.StartsWith(currentMonth));
            bool allFixedPaid = loadedData.FixedExpenses != null && loadedData.FixedExpenses.Count > 0 && loadedData.FixedExpenses.All(x => x.IsPaid);
            if (!hasCurrentMonthTxs && allFixedPaid)
            {
                isNewMonth = true;
            }

            FixedExpenses.Clear();
            _paidFixedSnapshot.Clear();

            var seenFe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (loadedData.FixedExpenses != null && loadedData.FixedExpenses.Count > 0)
            {
                foreach (var fe in loadedData.FixedExpenses)
                {
                    if (fe == null || string.IsNullOrWhiteSpace(fe.Name)) continue;
                    if (!seenFe.Add(fe.Name.Trim())) continue; // Skip duplicates
                    if (isNewMonth) fe.IsPaid = false;
                    FixedExpenses.Add(fe);
                    if (fe.IsPaid) _paidFixedSnapshot[fe] = (fe.Amount, 0m);
                }
            }
            if (FixedExpenses.Count == 0)
            {
                // 저장된 고정지출이 없거나 비어있는 경우 기본 필수 고정지출 5개 자동 복구
                AddDefaultFixedExpenses();
            }

            RefreshExpenseViews();
            return isNewMonth;
        }

        public void LoadData()
        {
            _isLoading = true;
            bool isNewMonth = false;
            try
            {
                AppData? loadedData = DatabaseService.IsConfigured ? DatabaseService.LoadFromDatabase() : null;

                if (loadedData == null && File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    loadedData = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
                }

                if (loadedData == null)
                {
                    loadedData = new AppData();
                }

                isNewMonth = ApplyData(loadedData);

                try
                {
                    string json = JsonSerializer.Serialize(loadedData, JsonOptions);
                    File.WriteAllText(SaveFilePath, json);
                    _lastSyncTime = File.GetLastWriteTimeUtc(SaveFilePath);
                }
                catch { }

                if (DatabaseService.IsConfigured)
                {
                    CloudSyncStatusText = $"✓ 동기화 완료 ({DateTime.Now:HH:mm:ss})";
                    IsCloudConnected = true;
                }
                else
                {
                    CloudSyncStatusText = "💾 로컬 저장 모드";
                    IsCloudConnected = false;
                }
            }
            catch
            {
                if (FixedExpenses.Count == 0) AddDefaultFixedExpenses();
                if (IncomePlans.Count == 0) AddDefaultIncomePlans();
                if (InvestmentPlans.Count == 0) AddDefaultInvestmentPlans();
                CloudSyncStatusText = DatabaseService.IsConfigured ? "⚠️ 로컬 캐시 모드" : "💾 로컬 저장 모드";
                IsCloudConnected = false;
            }
            finally
            {
                if (File.Exists(SaveFilePath))
                {
                    _lastSyncTime = File.GetLastWriteTimeUtc(SaveFilePath);
                }
                else
                {
                    _lastSyncTime = DateTime.UtcNow;
                }

                OnDefenderChanged();
                OnPlannerChanged();
                OnAnalyticsChanged();
                _isLoading = false;

                if (isNewMonth)
                {
                    SaveData();
                }
            }
        }

        public async Task RefreshFromCloudAsync(bool isManual = false)
        {
            if (_isLoading || _isSyncing) return;
            IsSyncing = true;

            if (!DatabaseService.IsConfigured)
            {
                try
                {
                    if (File.Exists(SaveFilePath))
                    {
                        string json = File.ReadAllText(SaveFilePath);
                        var localData = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
                        if (localData != null)
                        {
                            _isLoading = true;
                            ApplyData(localData);
                            OnDefenderChanged();
                            OnPlannerChanged();
                            OnAnalyticsChanged();
                            _isLoading = false;
                        }
                    }
                    CloudSyncStatusText = "💾 로컬 저장 모드";
                    IsCloudConnected = false;
                }
                catch { }
                finally
                {
                    _isLoading = false;
                    IsSyncing = false;
                }
                return;
            }

            if (isManual)
            {
                CloudSyncStatusText = "동기화 중...";
            }

            bool isNewMonth = false;
            try
            {
                var loadedData = await Task.Run(() => DatabaseService.LoadFromDatabase());
                if (loadedData != null)
                {
                    _isLoading = true;
                    isNewMonth = ApplyData(loadedData);

                    try
                    {
                        string json = JsonSerializer.Serialize(loadedData, JsonOptions);
                        File.WriteAllText(SaveFilePath, json);
                        _lastSyncTime = File.GetLastWriteTimeUtc(SaveFilePath);
                    }
                    catch { }

                    OnDefenderChanged();
                    OnPlannerChanged();
                    OnAnalyticsChanged();
                    _isLoading = false;

                    if (isNewMonth)
                    {
                        SaveData();
                    }

                    CloudSyncStatusText = $"✓ 동기화 완료 ({DateTime.Now:HH:mm:ss})";
                    IsCloudConnected = true;
                }
                else
                {
                    if (isManual)
                    {
                        CloudSyncStatusText = "⚠️ 동기화 실패 (네트워크 확인)";
                        IsCloudConnected = false;
                    }
                }
            }
            catch
            {
                if (isManual)
                {
                    CloudSyncStatusText = "⚠️ 동기화 오류";
                    IsCloudConnected = false;
                }
            }
            finally
            {
                _isLoading = false;
                IsSyncing = false;
            }
        }

        private void AddDefaultFixedExpenses()
        {
            FixedExpenses.Clear();
            _paidFixedSnapshot.Clear();
            var defaults = new (string name, decimal amount, bool isPaid)[]
            {
                ("통신비", 0m, false),
                ("구독료", 0m, false),
                ("관리비 / 공과금", 0m, false)
            };

            foreach (var d in defaults)
            {
                var item = new FixedExpenseItem { Name = d.name, Amount = d.amount, IsPaid = d.isPaid };
                FixedExpenses.Add(item);
                if (d.isPaid) _paidFixedSnapshot[item] = (d.amount, 0m);
            }
        }

        private void AddDefaultIncomePlans()
        {
            IncomePlans.Clear();
            IncomePlans.Add(new PlanItem { Name = "월급 / 주 수입", Amount = 0m });
            IncomePlans.Add(new PlanItem { Name = "보너스 / 부수입", Amount = 0m });
        }

        private void AddDefaultInvestmentPlans()
        {
            InvestmentPlans.Clear();
            InvestmentPlans.Add(new PlanItem { Name = "저축 / 투자", Amount = 0m });
            InvestmentPlans.Add(new PlanItem { Name = "카카오머니 (용돈)", Amount = 0m });
        }

        private void RefreshExpenseViews()
        {
            TodayExpenses.Clear();
            MonthlyHistoryExpenses.Clear();

            string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
            string currentMonth = DateTime.Today.ToString("yyyy-MM");

            foreach (var item in _allExpenses.Where(x => x.Date == todayStr).OrderByDescending(x => x.Time))
            {
                TodayExpenses.Add(item);
            }

            foreach (var item in _allExpenses.Where(x => x.Date.StartsWith(currentMonth)).OrderByDescending(x => x.Date).ThenByDescending(x => x.Time))
            {
                MonthlyHistoryExpenses.Add(item);
            }
        }

        public void SaveData()
        {
            if (_isLoading) return;
            try
            {
                var data = new AppData
                {
                    LastSavedDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    CurrentBank = this.CurrentBank,
                    TargetBalance = this.TargetBalance,
                    HoldAmount = this.HoldAmount,
                    IncludeUnpaidFixed = this.IncludeUnpaidFixed,
                    NaverPoints = this.NaverPoints,
                    KakaoPayMoney = this.KakaoPayMoney,
                    AllExpenseHistory = this._allExpenses,

                    LastActivateMonth = DateTime.Today.ToString("yyyy-MM"),
                    IncomePlans = [.. this.IncomePlans],
                    InvestmentPlans = [.. this.InvestmentPlans],
                    FixedExpenses = [.. this.FixedExpenses]
                };

                string json = JsonSerializer.Serialize(data, JsonOptions);
                File.WriteAllText(SaveFilePath, json);
                _lastSyncTime = File.GetLastWriteTimeUtc(SaveFilePath);

                // Save to Cloud DB asynchronously (Zero UI lag)
                if (DatabaseService.IsConfigured)
                {
                    DatabaseService.SaveToDatabaseAsync(data);
                }
            }
            catch { }
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
