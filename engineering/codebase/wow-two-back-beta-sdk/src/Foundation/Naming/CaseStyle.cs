namespace WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

/// <summary>Identifier casing styles produced and parsed by <see cref="CaseConverter"/>.</summary>
public enum CaseStyle
{
    /// <summary>Lowercase words joined by underscores. Example: <c>order_line_item</c>.</summary>
    Snake,

    /// <summary>Uppercase words joined by underscores. Example: <c>ORDER_LINE_ITEM</c>.</summary>
    ScreamingSnake,

    /// <summary>Lowercase words joined by hyphens. Example: <c>order-line-item</c>.</summary>
    Kebab,

    /// <summary>Capitalized words joined by hyphens. Example: <c>Order-Line-Item</c>.</summary>
    Train,

    /// <summary>First word lowercase, subsequent words capitalized, no separators. Example: <c>orderLineItem</c>.</summary>
    Camel,

    /// <summary>Every word capitalized, no separators. Example: <c>OrderLineItem</c>.</summary>
    Pascal,

    /// <summary>First word capitalized, the rest lowercase, space-separated. Example: <c>Order line item</c>.</summary>
    Sentence,

    /// <summary>Every word capitalized, space-separated. Example: <c>Order Line Item</c>.</summary>
    Title,

    /// <summary>All words lowercased, space-separated. Example: <c>order line item</c>.</summary>
    Lower,

    /// <summary>All words uppercased, space-separated. Example: <c>ORDER LINE ITEM</c>.</summary>
    Upper,
}
