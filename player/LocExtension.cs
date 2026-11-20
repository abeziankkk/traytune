using System.Windows.Markup;

namespace TrayTune;

/// <summary>
/// Подставляет перевод в XAML: <c>Text="{loc:Loc settings.title}"</c>.
/// Значение берётся один раз при загрузке окна — язык меняется перезапуском,
/// поэтому пересчитывать на лету не нужно.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
