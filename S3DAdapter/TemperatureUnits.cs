namespace S3DAdapter;

public static class TemperatureUnits
{
    // Target project's raw Smart 3D temperature values are Kelvin (owner corrected).
    // Do not round before rule evaluation or report export.
    public static decimal KelvinToCelsius(decimal kelvin) => kelvin - 273.15m;

    public static decimal? KelvinToCelsius(decimal? kelvin) =>
        kelvin.HasValue ? KelvinToCelsius(kelvin.Value) : (decimal?)null;
}
