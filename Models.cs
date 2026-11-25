using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BudgetSuite
{
    /// <summary>
    /// 개별 일일 거래 항목 (지출 또는 수입)
    /// </summary>
    public class ExpenseItem : INotifyPropertyChanged
    {
        private string _date = DateTime.Today.ToString("yyyy-MM-dd");
        private string _time = DateTime.Now.ToString("HH:mm");
        private string _itemType = "지출"; // "지출" 또는 "수입"
        private string _description = string.Empty;
        private decimal _amount;
        private string _paymentMethod = "현금/체크";

        public string Date
        {
            get => _date;
            set { _date = value; OnPropertyChanged(); }
        }

        public string Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged(); }
        }

        public string ItemType
        {
            get => _itemType;
            set
            {
                _itemType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsIncome));
                OnPropertyChanged(nameof(IsTransfer));
            }
        }

        public bool IsIncome => ItemType == "수입";
        public bool IsTransfer => ItemType == "이체";

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public decimal Amount
        {
            get => _amount;
            set { _amount = value; OnPropertyChanged(); }
        }

        public string PaymentMethod
        {
            get => _paymentMethod;
            set { _paymentMethod = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 월간 수당/수입 또는 투자 계획 개별 항목
    /// </summary>
    public class PlanItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private decimal _amount;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public decimal Amount
        {
            get => _amount;
            set { _amount = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 월간 고정 지출 항목 (이름, 금액, 납부완료 체크)
    /// </summary>
    public class FixedExpenseItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private decimal _amount;
        private bool _isPaid;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public decimal Amount
        {
            get => _amount;
            set { _amount = value; OnPropertyChanged(); }
        }

        public bool IsPaid
        {
            get => _isPaid;
            set { _isPaid = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 통합 데이터 저장 구조
    /// </summary>
    public class AppData
    {
        // === 일일 예산 방어기(Defender) 설정 ===
        public string LastSavedDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        public decimal CurrentBank { get; set; } = 0m;
        public decimal TargetBalance { get; set; } = 0m;
        public decimal HoldAmount { get; set; } = 0m;
        public bool IncludeUnpaidFixed { get; set; } = false;
        public decimal NaverPoints { get; set; } = 0m;
        public decimal KakaoPayMoney { get; set; } = 0m;
        public List<ExpenseItem> AllExpenseHistory { get; set; } = [];

        // === 월간 플래너(Manager) 설정 ===
        public string LastActivateMonth { get; set; } = DateTime.Today.ToString("yyyy-MM");
        
        // 동적 수당/수입 계획 목록
        public List<PlanItem> IncomePlans { get; set; } = [];

        // 동적 투자/저축 계획 목록
        public List<PlanItem> InvestmentPlans { get; set; } = [];

        // 고정 지출 목록
        public List<FixedExpenseItem> FixedExpenses { get; set; } = [];

        // 구 버전 데이터 역호환 필드 (단방향 이전용)
        public decimal JobAllowance { get; set; } = 0m;
        public decimal TrainingAllowance { get; set; } = 0m;
        public decimal InvestKiwoom { get; set; } = 0m;
        public decimal InvestKakao { get; set; } = 0m;
    }
}
