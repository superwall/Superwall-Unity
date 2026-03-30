using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class StoreProduct
    {
        public List<Entitlement> Entitlements;
        public string ProductIdentifier;
        public string SubscriptionGroupIdentifier;
        public Dictionary<string, string> Attributes;
        public string LocalizedPrice;
        public string LocalizedSubscriptionPeriod;
        public string Period;
        public string Periodly;
        public int PeriodWeeks;
        public string PeriodWeeksString;
        public int PeriodMonths;
        public string PeriodMonthsString;
        public int PeriodYears;
        public string PeriodYearsString;
        public int PeriodDays;
        public string PeriodDaysString;
        public string DailyPrice;
        public string WeeklyPrice;
        public string MonthlyPrice;
        public string YearlyPrice;
        public bool HasFreeTrial;
        public string TrialPeriodEndDate;
        public string TrialPeriodEndDateString;
        public string LocalizedTrialPeriodPrice;
        public double TrialPeriodPrice;
        public int TrialPeriodDays;
        public string TrialPeriodDaysString;
        public int TrialPeriodWeeks;
        public string TrialPeriodWeeksString;
        public int TrialPeriodMonths;
        public string TrialPeriodMonthsString;
        public int TrialPeriodYears;
        public string TrialPeriodYearsString;
        public string TrialPeriodText;
        public string Locale;
        public string LanguageCode;
        public string CurrencySymbol;
        public string CurrencyCode;
        public bool IsFamilyShareable;
        public string RegionCode;
        public double Price;
    }
}
