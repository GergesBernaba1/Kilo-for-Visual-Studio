# Quick Start: Using Copilot-Inspired Features

This guide shows how to use the new GitHub Copilot-inspired features in your Visual Studio 2022 Kilo extension.

## 1. Inline Ghost Text Completions

### Automatic Setup

The inline completion system is automatically activated via MEF when the extension loads. No manual initialization required!

### User Experience

1. **Start typing** code in any editor
2. **Wait 500ms** - debounce period
3. **Ghost text appears** - translucent gray suggestion at cursor
4. **Press Tab** - accept suggestion
5. **Press Esc** - dismiss suggestion
6. **Keep typing** - auto-dismisses and requests new suggestion

### Configuration

Control inline completions in `AutocompleteService`:

```csharp
// Enable/disable
autocompleteService.InlineCompletionEnabled = true;

// Adjust debounce timing (default 500ms)
// Edit in KiloInlineCompletionManager constructor:
private const int DebounceDelayMs = 500;
```

## 2. Applying Copilot Theme to Your Tool Windows

### Step 1: Reference the Theme

In your XAML file (e.g., `KiloAssistantToolWindowControl.xaml`):

```xaml
<UserControl x:Class="Kilo.VisualStudio.Extension.UI.YourControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="CopilotTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </UserControl.Resources>
    
    <!-- Your content here -->
</UserControl>
```

### Step 2: Use Themed Buttons

```xaml
<!-- Primary action button -->
<Button Style="{StaticResource CopilotButtonStyle}" Content="Send Message" Click="SendButton_Click"/>

<!-- Or customize colors -->
<Button Style="{StaticResource CopilotButtonStyle}" 
        Background="{StaticResource AccentBlue}" 
        Content="Refactor"/>
```

### Step 3: Use Themed TextBoxes

```xaml
<TextBox Style="{StaticResource CopilotTextBoxStyle}" 
         x:Name="PromptBox"
         MinHeight="80" 
         TextWrapping="Wrap" 
         AcceptsReturn="True"/>
```

## 3. Adding Message Bubbles to Chat UI

### Basic Usage

```csharp
using Kilo.VisualStudio.Extension.UI;

// User message
var userMessage = new MessageBubble
{
    MessageText = "How do I implement a singleton in C#?",
    IsUserMessage = true
};
chatStackPanel.Children.Add(userMessage);

// Assistant response
var assistantMessage = new MessageBubble
{
    MessageText = "Here's a thread-safe singleton implementation:\n\n```csharp\npublic sealed class Singleton { ... }```",
    IsUserMessage = false
};
chatStackPanel.Children.Add(assistantMessage);
```

### Streaming Responses

```csharp
// Show loading
var responseMessage = new MessageBubble
{
    MessageText = "",
    IsUserMessage = false,
    IsStreaming = true
};
chatStackPanel.Children.Add(responseMessage);

// As text arrives
assistantService.OnTextDelta += (sender, delta) =>
{
    Dispatcher.Invoke(() =>
    {
        responseMessage.MessageText += delta;
    });
};

// When complete
assistantService.OnComplete += (sender, response) =>
{
    Dispatcher.Invoke(() =>
    {
        responseMessage.IsStreaming = false;
    });
};
```

### Typewriter Effect (Optional)

```csharp
var message = new MessageBubble
{
    IsUserMessage = false
};
chatStackPanel.Children.Add(message);

// Animate text character by character
message.AnimateTextTypewriter("This text will appear one character at a time!", delayMs: 15);
```

## 4. Adding Loading Indicators

```csharp
using Kilo.VisualStudio.Extension.UI;

// Show loading
var loadingIndicator = new LoadingAnimation();
statusPanel.Children.Add(loadingIndicator);

// When operation completes
await DoSomethingAsync();
loadingIndicator.StopAnimation();
statusPanel.Children.Remove(loadingIndicator);
```

## 5. Manually Triggering Ghost Text

Although ghost text is automatic, you can manually control it:

```csharp
// Get the adornment from text view
if (textView.Properties.TryGetProperty(typeof(KiloInlineGhostTextAdornment), out KiloInlineGhostTextAdornment adornment))
{
    // Show a suggestion
    var cursorPosition = textView.Caret.Position.BufferPosition;
    adornment.ShowSuggestion("Console.WriteLine(\"Hello, World!\");", cursorPosition);
    
    // Hide current suggestion
    adornment.HideSuggestion();
    
    // Accept programmatically
    bool accepted = adornment.AcceptSuggestion();
}
```

## 6. Customizing Colors

### Method 1: Override in Your XAML

```xaml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="CopilotTheme.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        
        <!-- Override colors -->
        <SolidColorBrush x:Key="AccentPurple" Color="#8B5CF6"/>
        <SolidColorBrush x:Key="AccentGreen" Color="#10B981"/>
    </ResourceDictionary>
</UserControl.Resources>
```

### Method 2: Edit CopilotTheme.xaml

Find the color definitions at the top of `CopilotTheme.xaml`:

```xaml
<Color x:Key="CopilotPurple">#6E40C9</Color>  <!-- Change this -->
<Color x:Key="CopilotBlue">#0969DA</Color>
```

## 7. Keyboard Shortcuts

Built-in keyboard shortcuts for inline completions:

- **Tab** - Accept ghost text suggestion
- **Esc** - Dismiss ghost text suggestion
- **Any arrow key** - Auto-dismiss and move cursor
- **Any typing** - Auto-dismiss and replace with new character

To customize, edit `KiloInlineGhostTextController.cs`:

```csharp
private void OnPreviewKeyDown(object sender, KeyEventArgs e)
{
    if (!_adornment.IsVisible)
        return;

    // Your custom key handling
    if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
    {
        // Ctrl+Enter for partial acceptance, etc.
    }
}
```

## 8. Debugging Tips

### Enable Logging

```csharp
// In KiloInlineCompletionManager
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"Inline completion error: {ex.Message}");
    // Or use your logger:
    _logger.Error($"Inline completion failed: {ex}");
}
```

### Test Without Backend

Set mock mode in settings:

```csharp
var settings = ExtensionSettings.Load();
settings.UseMockBackend = true;
settings.Save();
```

### Inspect Adornment Layer

Use Visual Studio's WPF Inspector:
1. Debug the extension
2. Use Snoop or Live Visual Tree
3. Find "KiloInlineGhostText" adornment layer
4. Inspect opacity, position, colors

## 9. Performance Tuning

### Adjust Debounce Delay

Edit `KiloInlineCompletionManager.cs`:

```csharp
private const int DebounceDelayMs = 500; // Increase for slower requests
```

### Limit Context Size

Edit context lines in `KiloInlineCompletionManager.cs`:

```csharp
var contextLinesBefore = Math.Min(20, line.LineNumber);      // Reduce to 10
var contextLinesAfter = Math.Min(5, snapshot.LineCount - line.LineNumber - 1); // Reduce to 3
```

### Cancel Stale Requests

The system automatically cancels outdated requests. No action needed!

## 10. Common Issues

### Ghost Text Not Appearing

1. Check `AutocompleteService.InlineCompletionEnabled` is true
2. Verify backend is responding (check mock mode)
3. Ensure you're typing in a document (not immediate window)
4. Wait for full debounce period (500ms)

### Animations Not Smooth

1. Check WPF rendering tier on the machine
2. Reduce animation durations in CopilotTheme.xaml
3. Disable animations if performance is poor

### Theme Colors Not Applied

1. Verify CopilotTheme.xaml is included in project
2. Check resource dictionary merge order
3. Ensure build action is "Resource" or "Page"

## Example: Complete Chat Implementation

```csharp
public class CopilotChatPanel : UserControl
{
    private StackPanel chatPanel;
    private TextBox inputBox;
    private Button sendButton;
    private AssistantService assistantService;

    public CopilotChatPanel(AssistantService service)
    {
        assistantService = service;
        InitializeUI();
        HookupEvents();
    }

    private void InitializeUI()
    {
        // Load theme
        var theme = new ResourceDictionary
        {
            Source = new Uri("/Kilo.VisualStudio.Extension;component/UI/CopilotTheme.xaml", UriKind.Relative)
        };
        Resources.MergedDictionaries.Add(theme);

        // Create chat panel
        chatPanel = new StackPanel { Margin = new Thickness(12) };
        var scrollViewer = new ScrollViewer { Content = chatPanel };

        // Create input
        inputBox = new TextBox { Style = (Style)FindResource("CopilotTextBoxStyle") };
        sendButton = new Button 
        { 
            Style = (Style)FindResource("CopilotButtonStyle"),
            Content = "Send"
        };
        sendButton.Click += SendButton_Click;

        // Layout
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(scrollViewer, 0);
        Grid.SetRow(inputBox, 1);
        grid.Children.Add(scrollViewer);
        grid.Children.Add(inputBox);
        grid.Children.Add(sendButton);

        Content = grid;
    }

    private void HookupEvents()
    {
        assistantService.OnTextDelta += (s, delta) =>
        {
            Dispatcher.Invoke(() =>
            {
                var lastMessage = chatPanel.Children.OfType<MessageBubble>().LastOrDefault();
                if (lastMessage != null && !lastMessage.IsUserMessage)
                {
                    lastMessage.MessageText += delta;
                }
            });
        };
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var prompt = inputBox.Text;
        if (string.IsNullOrWhiteSpace(prompt)) return;

        // Add user message
        chatPanel.Children.Add(new MessageBubble
        {
            MessageText = prompt,
            IsUserMessage = true
        });

        // Clear input
        inputBox.Clear();

        // Add loading response
        var responseMessage = new MessageBubble
        {
            MessageText = "",
            IsUserMessage = false,
            IsStreaming = true
        };
        chatPanel.Children.Add(responseMessage);

        // Send to assistant
        try
        {
            await assistantService.SendMessage(prompt);
            responseMessage.IsStreaming = false;
        }
        catch (Exception ex)
        {
            responseMessage.MessageText = $"Error: {ex.Message}";
            responseMessage.IsStreaming = false;
        }
    }
}
```

## Summary

You now have:
- ✅ Inline ghost text completions (automatic)
- ✅ Copilot-themed UI components
- ✅ Animated message bubbles
- ✅ Loading indicators
- ✅ Full keyboard control

Enjoy building your Copilot-inspired extension! 🚀
