﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace BSA_Browser.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "16.7.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("C:\\")]
        public string LastUnpackPath {
            get {
                return ((string)(this["LastUnpackPath"]));
            }
            set {
                this["LastUnpackPath"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<ArrayOfString xmlns:xsi=\"http://www.w3." +
            "org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" />")]
        public global::System.Collections.Specialized.StringCollection RecentFiles {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["RecentFiles"]));
            }
            set {
                this["RecentFiles"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("30")]
        public int RecentFiles_MaxFiles {
            get {
                return ((int)(this["RecentFiles_MaxFiles"]));
            }
            set {
                this["RecentFiles_MaxFiles"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool UpdateSettings {
            get {
                return ((bool)(this["UpdateSettings"]));
            }
            set {
                this["UpdateSettings"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::BSA_Browser.Classes.WindowStates WindowStates {
            get {
                return ((global::BSA_Browser.Classes.WindowStates)(this["WindowStates"]));
            }
            set {
                this["WindowStates"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool SortDesc {
            get {
                return ((bool)(this["SortDesc"]));
            }
            set {
                this["SortDesc"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("FilePath")]
        public global::BSA_Browser.ArchiveFileSortOrder SortType {
            get {
                return ((global::BSA_Browser.ArchiveFileSortOrder)(this["SortType"]));
            }
            set {
                this["SortType"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::BSA_Browser.Classes.QuickExtractPaths QuickExtractPaths {
            get {
                return ((global::BSA_Browser.Classes.QuickExtractPaths)(this["QuickExtractPaths"]));
            }
            set {
                this["QuickExtractPaths"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool SortArchiveDirectories {
            get {
                return ((bool)(this["SortArchiveDirectories"]));
            }
            set {
                this["SortArchiveDirectories"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool SearchUseRegex {
            get {
                return ((bool)(this["SearchUseRegex"]));
            }
            set {
                this["SearchUseRegex"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string OpenArchiveDialog {
            get {
                return ((string)(this["OpenArchiveDialog"]));
            }
            set {
                this["OpenArchiveDialog"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool RetrieveRealSize {
            get {
                return ((bool)(this["RetrieveRealSize"]));
            }
            set {
                this["RetrieveRealSize"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("65000")]
        public int EncodingCodePage {
            get {
                return ((int)(this["EncodingCodePage"]));
            }
            set {
                this["EncodingCodePage"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool CheckForUpdates {
            get {
                return ((bool)(this["CheckForUpdates"]));
            }
            set {
                this["CheckForUpdates"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("FileList, FolderTree")]
        public global::BSA_Browser.Enums.Icons Icons {
            get {
                return ((global::BSA_Browser.Enums.Icons)(this["Icons"]));
            }
            set {
                this["Icons"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(@"<?xml version=""1.0"" encoding=""utf-16""?>
<ArrayOfString xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <string>.dds</string>
  <string>.bmp</string>
  <string>.png</string>
  <string>.jpg</string>
  <string>.txt</string>
  <string>.bat</string>
  <string>.xml</string>
  <string>.lst</string>
  <string>.psc</string>
  <string>.json</string>
</ArrayOfString>")]
        public global::System.Collections.Specialized.StringCollection BuiltInPreviewing {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["BuiltInPreviewing"]));
            }
            set {
                this["BuiltInPreviewing"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ReplaceGNFExt {
            get {
                return ((bool)(this["ReplaceGNFExt"]));
            }
            set {
                this["ReplaceGNFExt"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MatchLastWriteTime {
            get {
                return ((bool)(this["MatchLastWriteTime"]));
            }
            set {
                this["MatchLastWriteTime"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool CompareFilterUnique {
            get {
                return ((bool)(this["CompareFilterUnique"]));
            }
            set {
                this["CompareFilterUnique"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool CompareFilterDifferent {
            get {
                return ((bool)(this["CompareFilterDifferent"]));
            }
            set {
                this["CompareFilterDifferent"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool CompareFilterIdentical {
            get {
                return ((bool)(this["CompareFilterIdentical"]));
            }
            set {
                this["CompareFilterIdentical"] = value;
            }
        }
    }
}
