namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Defines the geometry of each data module — the code body, excluding the three finder eyes — where <see cref="Square"/> is the byte-parity default and every other value floors ECC to <see cref="EccLevel.Q"/>.</summary>
public enum ModuleShape
{
    /// <summary>Filled squares merged into horizontal runs — the default.</summary>
    Square,

    /// <summary>Each module a rounded-rect, drawn independently with gaps between modules.</summary>
    Rounded,

    /// <summary>Each module a circle inscribed in its cell — the classic dotted look.</summary>
    Dots,

    /// <summary>Each module a quarter-rounded tile that rounds only the corners with no orthogonal neighbour, giving a connected body.</summary>
    Classy,

    /// <summary>Like <see cref="Classy"/> but with a larger corner radius — a softer connected body.</summary>
    ClassyRounded,

    /// <summary>Contiguous modules in each column merged into a single rounded vertical bar.</summary>
    VerticalBars,

    /// <summary>Contiguous modules in each row merged into a single rounded horizontal bar.</summary>
    HorizontalBars,
}
