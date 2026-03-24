using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;
using Microsoft.VisualStudio.Shell;

namespace Kilo.VisualStudio.Extension.UI
{
    public partial class KiloAutomationToolWindowControl : UserControl
    {
        private AutomationService? _automationService;
        private MacroRecorderService? _macroRecorderService;
        private AutomationTemplate? _selectedTemplate;

        public KiloAutomationToolWindowControl()
        {
            InitializeComponent();
            InitializeServices();
            LoadTemplates();
        }

        private void InitializeServices()
        {
            _automationService = KiloPackage.AutomationServiceInstance;
            if (_automationService != null)
            {
                _automationService.TemplatesChanged += OnTemplatesChanged;
            }
        }

        private void LoadTemplates()
        {
            if (_automationService == null) return;

            TemplatesListBox.ItemsSource = _automationService.Templates;
            UpdateUI();
        }

        private void UpdateUI()
        {
            var hasSelection = _selectedTemplate != null;
            ExecuteButton.IsEnabled = hasSelection;
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
            ExportButton.IsEnabled = hasSelection;

            if (_selectedTemplate != null)
            {
                TemplateNameTextBox.Text = _selectedTemplate.Name;
                TemplateDescriptionTextBox.Text = _selectedTemplate.Description;
                TemplateCategoryComboBox.Text = _selectedTemplate.Category;
                StepsListBox.ItemsSource = _selectedTemplate.Steps;
            }
            else
            {
                TemplateNameTextBox.Text = "";
                TemplateDescriptionTextBox.Text = "";
                TemplateCategoryComboBox.Text = "";
                StepsListBox.ItemsSource = null;
            }
        }

        private void OnTemplatesChanged(object? sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LoadTemplates();
            });
        }

        private void OnRecordingStateChanged(object? sender, bool isRecording)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                StopRecordingButton.IsEnabled = isRecording;
                RecordMacroButton.IsEnabled = !isRecording;
                RecordingStatusTextBlock.Text = isRecording ? "RECORDING..." : "";
                StatusTextBlock.Text = isRecording ? "Recording macro actions..." : "Ready";
            });
        }

        private void OnStepRecorded(object? sender, AutomationStep step)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                StatusTextBlock.Text = $"Recorded: {step.Description}";
            });
        }

        private void NewTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            var template = new AutomationTemplate
            {
                Name = "New Template",
                Description = "Description",
                Category = "General",
                Steps = new List<AutomationStep>()
            };

            _automationService?.AddTemplate(template);
            TemplatesListBox.SelectedItem = template;
            _selectedTemplate = template;
            UpdateUI();
        }

        private void RecordMacroButton_Click(object sender, RoutedEventArgs e)
        {
            if (_macroRecorderService == null)
            {
                var dte = (EnvDTE.DTE?)Package.GetGlobalService(typeof(EnvDTE.DTE));
                if (dte != null)
                {
                    _macroRecorderService = new MacroRecorderService(dte);
                    _macroRecorderService.RecordingStateChanged += OnRecordingStateChanged;
                    _macroRecorderService.StepRecorded += OnStepRecorded;
                }
            }

            _macroRecorderService?.StartRecording();
        }

        private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_macroRecorderService == null) return;

            var template = _macroRecorderService.StopRecording("Recorded Macro", "Macro recorded from user actions");
            if (template != null)
            {
                _automationService?.AddTemplate(template);
                TemplatesListBox.SelectedItem = template;
                _selectedTemplate = template;
                UpdateUI();
            }
        }

        private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null || _automationService == null) return;

            StatusTextBlock.Text = "Executing template...";

            try
            {
                var context = new AutomationContext
                {
                    ActiveFilePath = "", // TODO: Get from VS
                    SelectedText = "",
                    LanguageId = "",
                    WorkspaceRoot = "" // TODO: Get workspace root
                };

                var result = await _automationService.ExecuteTemplateAsync(_selectedTemplate.Id, context);

                StatusTextBlock.Text = result.Success ? "Execution completed" : $"Execution failed: {result.Message}";

                // Show results
                var resultMessage = $"Template '{_selectedTemplate.Name}' executed.\n\nSteps executed:\n" +
                    string.Join("\n", result.StepsExecuted);

                MessageBox.Show(resultMessage, "Execution Results",
                    MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Execution error: {ex.Message}";
                MessageBox.Show($"Error executing template: {ex.Message}", "Execution Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null || _automationService == null) return;

            _selectedTemplate.Name = TemplateNameTextBox.Text;
            _selectedTemplate.Description = TemplateDescriptionTextBox.Text;
            _selectedTemplate.Category = TemplateCategoryComboBox.Text;

            _automationService.UpdateTemplate(_selectedTemplate);
            LoadTemplates(); // Refresh the list
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null || _automationService == null) return;

            var result = MessageBox.Show($"Delete template '{_selectedTemplate.Name}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _automationService.RemoveTemplate(_selectedTemplate.Id);
                _selectedTemplate = null;
                UpdateUI();
            }
        }

        private void TemplatesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedTemplate = TemplatesListBox.SelectedItem as AutomationTemplate;
            UpdateUI();
        }

        private void AddStepButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null) return;

            // TODO: Show step editor dialog
            var step = new AutomationStep
            {
                Type = AutomationStepType.RunCommand,
                Description = "New step",
                Parameters = new Dictionary<string, string>()
            };

            _selectedTemplate.Steps.Add(step);
            StepsListBox.Items.Refresh();
        }

        private void EditStepButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement step editing
        }

        private void RemoveStepButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null || StepsListBox.SelectedItem == null) return;

            var step = StepsListBox.SelectedItem as AutomationStep;
            if (step != null)
            {
                _selectedTemplate.Steps.Remove(step);
                StepsListBox.Items.Refresh();
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null || StepsListBox.SelectedItem == null) return;

            var step = StepsListBox.SelectedItem as AutomationStep;
            var index = _selectedTemplate.Steps.IndexOf(step);
            if (index > 0)
            {
                _selectedTemplate.Steps.RemoveAt(index);
                _selectedTemplate.Steps.Insert(index - 1, step);
                StepsListBox.Items.Refresh();
                StepsListBox.SelectedItem = step;
            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null || StepsListBox.SelectedItem == null) return;

            var step = StepsListBox.SelectedItem as AutomationStep;
            var index = _selectedTemplate.Steps.IndexOf(step);
            if (index < _selectedTemplate.Steps.Count - 1)
            {
                _selectedTemplate.Steps.RemoveAt(index);
                _selectedTemplate.Steps.Insert(index + 1, step);
                StepsListBox.Items.Refresh();
                StepsListBox.SelectedItem = step;
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement template import
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement template export
        }
    }
}