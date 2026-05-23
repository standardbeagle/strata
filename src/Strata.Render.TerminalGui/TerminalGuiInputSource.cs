using Strata.Interaction;
using Terminal.Gui;

namespace Strata.Render.TerminalGui;

/// <summary>
/// The live terminal input source the Phase 6 re-scope deferred to Phase 7. Adapts Terminal.Gui v2
/// keystrokes into Strata <see cref="HostEvent.Key"/>s and pushes them onto an
/// <see cref="IInputSource"/>, so the existing <c>InteractionHost</c> dispatcher,
/// <c>FocusController</c>, and <c>SelectionController</c> drive a full-screen app from real
/// keyboard input — the same interaction layer <c>Format-Styled -Interactive</c> drove
/// programmatically.
/// </summary>
/// <remarks>
/// <para>
/// Wire <see cref="HandleKey"/> to a Terminal.Gui <see cref="View.KeyDown"/> event (or call it from
/// a top-level key handler). It translates the key into the closed event-name DSL the
/// <c>command:</c> property and <c>KeyBindingMap</c> already speak — <c>key.j</c>, <c>key.k</c>,
/// <c>key.ArrowDown</c>, <c>key.ctrl+c</c>, <c>key.space</c> — and pushes a
/// <see cref="HostEvent.Key"/> carrying an equivalent <see cref="ConsoleKeyInfo"/>.
/// </para>
/// <para>
/// The naming mirrors the conventions already used across the interaction tests and samples:
/// a printable letter becomes its lowercase form (<c>key.j</c>); cursor keys become
/// <c>key.ArrowUp/Down/Left/Right</c>; space becomes <c>key.space</c>; modifiers prefix the key
/// (<c>key.ctrl+c</c>, <c>key.alt+x</c>). The dispatcher matches that name against active bindings.
/// </para>
/// </remarks>
public sealed class TerminalGuiInputSource : IInputSource, IDisposable
{
    private readonly InputSource _inner = new();

    /// <inheritdoc/>
    public IObservable<HostEvent> Events => _inner.Events;

    /// <summary>
    /// Translate a Terminal.Gui keystroke into a Strata <see cref="HostEvent.Key"/> and push it onto
    /// the input stream. Returns the event name pushed (e.g. <c>key.j</c>), or <see langword="null"/>
    /// if the key carried no mappable token (in which case nothing is pushed).
    /// </summary>
    public string? HandleKey(Key key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var token = Token(key);
        if (token is null)
        {
            return null;
        }

        var name = "key." + Prefix(key) + token;
        _inner.Push(new HostEvent.Key(name, ToConsoleKeyInfo(key, token)));
        return name;
    }

    private static string Prefix(Key key)
    {
        // Modifier order is fixed (ctrl, then alt) so a given chord always yields one stable name.
        var prefix = string.Empty;
        if (key.IsCtrl)
        {
            prefix += "ctrl+";
        }

        if (key.IsAlt)
        {
            prefix += "alt+";
        }

        return prefix;
    }

    private static string? Token(Key key)
    {
        // Named keys first; their event tokens match the interaction layer's existing conventions.
        switch (key.KeyCode)
        {
            case KeyCode.CursorUp:
                return "ArrowUp";
            case KeyCode.CursorDown:
                return "ArrowDown";
            case KeyCode.CursorLeft:
                return "ArrowLeft";
            case KeyCode.CursorRight:
                return "ArrowRight";
            case KeyCode.Space:
                return "space";
            case KeyCode.Enter:
                return "enter";
            case KeyCode.Esc:
                return "esc";
            case KeyCode.Tab:
                return "tab";
        }

        // A-Z letter keys (including chords like Ctrl+C, where AsRune collapses to a control rune)
        // map by their key code so "key.ctrl+c" resolves the same as a bare "key.c". The base letter
        // is the char-mask portion of the key code; modifiers live in the high bits.
        var letter = (uint)(key.KeyCode & KeyCode.CharMask);
        if (letter is >= (uint)KeyCode.A and <= (uint)KeyCode.Z)
        {
            return char.ToLowerInvariant((char)letter).ToString();
        }

        // Other printable characters (digits, punctuation) become their lowercase form.
        var rune = key.AsRune;
        if (rune.Value != 0 && !char.IsControl((char)Math.Min(rune.Value, char.MaxValue)))
        {
            return char.ToLowerInvariant((char)rune.Value).ToString();
        }

        return null;
    }

    private static ConsoleKeyInfo ToConsoleKeyInfo(Key key, string token)
    {
        var keyChar = token.Length == 1 ? token[0] : '\0';
        var consoleKey = key.KeyCode switch
        {
            KeyCode.CursorUp => ConsoleKey.UpArrow,
            KeyCode.CursorDown => ConsoleKey.DownArrow,
            KeyCode.CursorLeft => ConsoleKey.LeftArrow,
            KeyCode.CursorRight => ConsoleKey.RightArrow,
            KeyCode.Space => ConsoleKey.Spacebar,
            KeyCode.Enter => ConsoleKey.Enter,
            KeyCode.Esc => ConsoleKey.Escape,
            KeyCode.Tab => ConsoleKey.Tab,
            _ => keyChar is >= 'a' and <= 'z' ? ConsoleKey.A + (keyChar - 'a') : default,
        };

        return new ConsoleKeyInfo(keyChar, consoleKey, key.IsShift, key.IsAlt, key.IsCtrl);
    }

    /// <summary>Complete the underlying stream and release subscribers.</summary>
    public void Dispose() => _inner.Dispose();
}
