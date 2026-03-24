using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kilo.VisualStudio.App.Services
{
    public class LocalizationService
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "en", new Dictionary<string, string>
                {
                    { "app.title", "Kilo for Visual Studio" },
                    { "menu.kilo", "Kilo Assistant" },
                    { "menu.openAssistant", "Open Kilo Assistant" },
                    { "menu.askSelection", "Ask About Selection" },
                    { "menu.askFile", "Ask About Current File" },
                    { "menu.settings", "Settings" },
                    { "menu.sessionHistory", "Session History" },
                    { "menu.newSession", "New Session" },
                    { "window.assistant", "Kilo Assistant" },
                    { "window.diffViewer", "Kilo Diff Viewer" },
                    { "window.sessionHistory", "Session History" },
                    { "window.settings", "Kilo Settings" },
                    { "status.connected", "Connected" },
                    { "status.disconnected", "Disconnected" },
                    { "status.connecting", "Connecting..." },
                    { "button.send", "Send" },
                    { "button.clear", "Clear" },
                    { "button.apply", "Apply" },
                    { "button.revert", "Revert" },
                    { "button.save", "Save" },
                    { "button.cancel", "Cancel" },
                    { "button.delete", "Delete" },
                    { "button.refresh", "Refresh" },
                    { "label.prompt", "Enter your prompt..." },
                    { "label.mode", "Mode" },
                    { "label.session", "Session" },
                    { "label.status", "Status" },
                    { "label.profile", "Profile" },
                    { "label.model", "Model" },
                    { "label.provider", "Provider" },
                    { "label.apiKey", "API Key" },
                    { "error.connectionFailed", "Connection failed" },
                    { "error.invalidApiKey", "Invalid API key" },
                    { "error.sessionFailed", "Session failed" },
                    { "success.toolApplied", "Tool applied successfully" },
                    { "success.sessionCreated", "Session created" }
                }
            },
            {
                "es", new Dictionary<string, string>
                {
                    { "app.title", "Kilo para Visual Studio" },
                    { "menu.kilo", "Asistente Kilo" },
                    { "menu.openAssistant", "Abrir Asistente Kilo" },
                    { "menu.askSelection", "Preguntar sobre Selección" },
                    { "menu.askFile", "Preguntar sobre Archivo Actual" },
                    { "menu.settings", "Configuración" },
                    { "menu.sessionHistory", "Historial de Sesiones" },
                    { "menu.newSession", "Nueva Sesión" },
                    { "window.assistant", "Asistente Kilo" },
                    { "window.diffViewer", "Visor de Diferencias Kilo" },
                    { "window.sessionHistory", "Historial de Sesiones" },
                    { "window.settings", "Configuración Kilo" },
                    { "status.connected", "Conectado" },
                    { "status.disconnected", "Desconectado" },
                    { "status.connecting", "Conectando..." },
                    { "button.send", "Enviar" },
                    { "button.clear", "Limpiar" },
                    { "button.apply", "Aplicar" },
                    { "button.revert", "Revertir" },
                    { "button.save", "Guardar" },
                    { "button.cancel", "Cancelar" },
                    { "button.delete", "Eliminar" },
                    { "button.refresh", "Actualizar" },
                    { "label.prompt", "Ingrese su solicitud..." },
                    { "label.mode", "Modo" },
                    { "label.session", "Sesión" },
                    { "label.status", "Estado" },
                    { "label.profile", "Perfil" },
                    { "label.model", "Modelo" },
                    { "label.provider", "Proveedor" },
                    { "label.apiKey", "Clave API" },
                    { "error.connectionFailed", "Conexión fallida" },
                    { "error.invalidApiKey", "Clave API inválida" },
                    { "error.sessionFailed", "Sesión fallida" },
                    { "success.toolApplied", "Herramienta aplicada correctamente" },
                    { "success.sessionCreated", "Sesión creada" }
                }
            },
            {
                "fr", new Dictionary<string, string>
                {
                    { "app.title", "Kilo pour Visual Studio" },
                    { "menu.kilo", "Assistant Kilo" },
                    { "menu.openAssistant", "Ouvrir Assistant Kilo" },
                    { "menu.askSelection", "Demander sur la Sélection" },
                    { "menu.askFile", "Demander sur le Fichier Actuel" },
                    { "menu.settings", "Paramètres" },
                    { "menu.sessionHistory", "Historique des Sessions" },
                    { "menu.newSession", "Nouvelle Session" },
                    { "window.assistant", "Assistant Kilo" },
                    { "window.diffViewer", "Visionneur de Différences Kilo" },
                    { "window.sessionHistory", "Historique des Sessions" },
                    { "window.settings", "Paramètres Kilo" },
                    { "status.connected", "Connecté" },
                    { "status.disconnected", "Déconnecté" },
                    { "status.connecting", "Connexion..." },
                    { "button.send", "Envoyer" },
                    { "button.clear", "Effacer" },
                    { "button.apply", "Appliquer" },
                    { "button.revert", "Restaurer" },
                    { "button.save", "Enregistrer" },
                    { "button.cancel", "Annuler" },
                    { "button.delete", "Supprimer" },
                    { "button.refresh", "Actualiser" },
                    { "label.prompt", "Entrez votre demande..." },
                    { "label.mode", "Mode" },
                    { "label.session", "Session" },
                    { "label.status", "Statut" },
                    { "label.profile", "Profil" },
                    { "label.model", "Modèle" },
                    { "label.provider", "Fournisseur" },
                    { "label.apiKey", "Clé API" },
                    { "error.connectionFailed", "Échec de connexion" },
                    { "error.invalidApiKey", "Clé API invalide" },
                    { "error.sessionFailed", "Échec de session" },
                    { "success.toolApplied", "Outil appliqué avec succès" },
                    { "success.sessionCreated", "Session créée" }
                }
            },
            {
                "de", new Dictionary<string, string>
                {
                    { "app.title", "Kilo für Visual Studio" },
                    { "menu.kilo", "Kilo-Assistent" },
                    { "menu.openAssistant", "Kilo-Assistent öffnen" },
                    { "menu.askSelection", "Zur Auswahl fragen" },
                    { "menu.askFile", "Zur aktuellen Datei fragen" },
                    { "menu.settings", "Einstellungen" },
                    { "menu.sessionHistory", "Sitzungsverlauf" },
                    { "menu.newSession", "Neue Sitzung" },
                    { "window.assistant", "Kilo-Assistent" },
                    { "window.diffViewer", "Kilo Diff-Viewer" },
                    { "window.sessionHistory", "Sitzungsverlauf" },
                    { "window.settings", "Kilo-Einstellungen" },
                    { "status.connected", "Verbunden" },
                    { "status.disconnected", "Getrennt" },
                    { "status.connecting", "Verbinden..." },
                    { "button.send", "Senden" },
                    { "button.clear", "Löschen" },
                    { "button.apply", "Anwenden" },
                    { "button.revert", "Zurücksetzen" },
                    { "button.save", "Speichern" },
                    { "button.cancel", "Abbrechen" },
                    { "button.delete", "Löschen" },
                    { "button.refresh", "Aktualisieren" },
                    { "label.prompt", "Eingabeaufforderung eingeben..." },
                    { "label.mode", "Modus" },
                    { "label.session", "Sitzung" },
                    { "label.status", "Status" },
                    { "label.profile", "Profil" },
                    { "label.model", "Modell" },
                    { "label.provider", "Anbieter" },
                    { "label.apiKey", "API-Schlüssel" },
                    { "error.connectionFailed", "Verbindung fehlgeschlagen" },
                    { "error.invalidApiKey", "Ungültiger API-Schlüssel" },
                    { "error.sessionFailed", "Sitzung fehlgeschlagen" },
                    { "success.toolApplied", "Werkzeug erfolgreich angewendet" },
                    { "success.sessionCreated", "Sitzung erstellt" }
                }
            }
        };

        private string _currentLanguage = "en";

        public string CurrentLanguage => _currentLanguage;

        public IReadOnlyList<string> AvailableLanguages => new List<string>(Translations.Keys);

        public void SetLanguage(string languageCode)
        {
            if (Translations.ContainsKey(languageCode))
            {
                _currentLanguage = languageCode;
            }
            else
            {
                _currentLanguage = "en";
            }
        }

        public void SetLanguageFromSystem()
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (Translations.ContainsKey(culture))
            {
                _currentLanguage = culture;
            }
            else
            {
                _currentLanguage = "en";
            }
        }

        public string Get(string key)
        {
            if (Translations.TryGetValue(_currentLanguage, out var langDict))
            {
                if (langDict.TryGetValue(key, out var value))
                {
                    return value;
                }
            }

            if (Translations.TryGetValue("en", out var enDict))
            {
                if (enDict.TryGetValue(key, out var value))
                {
                    return value;
                }
            }

            return key;
        }

        public string Get(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        public string GetLanguageDisplayName(string languageCode)
        {
            return languageCode switch
            {
                "en" => "English",
                "es" => "Español",
                "fr" => "Français",
                "de" => "Deutsch",
                _ => languageCode
            };
        }
    }
}