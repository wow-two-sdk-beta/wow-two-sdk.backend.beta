namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Refers to the calendar window a quota counts in, in UTC.</summary>
public enum QuotaPeriod
{
    /// <summary>Refers to a UTC day.</summary>
    Day,

    /// <summary>Refers to an ISO week, Monday to Sunday.</summary>
    Week,

    /// <summary>Refers to a calendar month.</summary>
    Month,

    /// <summary>Refers to a calendar year.</summary>
    Year,

    /// <summary>Refers to no window: the count never resets.</summary>
    Total,
}
