import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]


class OperationalHubContractTests(unittest.TestCase):
    def read(self, relative: str) -> str:
        return (ROOT / relative).read_text(encoding="utf-8")

    def test_windows_exposes_all_operational_surfaces(self):
        shell = self.read("src/NemoclawChat.Windows/MainWindow.xaml.cs")
        for page in (
            "ProjectsPage",
            "ArtifactsPage",
            "SearchPage",
            "ContinuityPage",
            "AuditPage",
        ):
            with self.subTest(page=page):
                self.assertIn(f"typeof({page})", shell)

        archive = self.read("src/NemoclawChat.Windows/Pages/ArchivePage.xaml.cs")
        self.assertIn("ConversationManagerPage", archive)

    def test_gateway_exposes_typed_server_control_and_audit(self):
        patcher = self.read("scripts/patch-hermes-gateway-native.py")
        for route in (
            "/v1/hub/server/control",
            "/v1/hub/server/action",
            "/v1/hub/server/maintenance",
            "/v1/hub/audit",
        ):
            with self.subTest(route=route):
                self.assertIn(route, patcher)
        self.assertIn("HERMES_HUB_UPDATE_COMMAND", patcher)
        self.assertIn("_hermes_hub_audit_event", patcher)

    def test_advanced_conversation_and_export_contracts_exist(self):
        archive = self.read("src/NemoclawChat.Windows/Services/ChatArchiveStore.cs")
        manager = self.read("src/NemoclawChat.Windows/Pages/ConversationManagerPage.xaml.cs")
        exporter = self.read("src/NemoclawChat.Windows/Services/ConversationExportService.cs")
        self.assertIn("CreateBranch", archive)
        self.assertIn("ToggleBookmark", archive)
        self.assertIn("DeleteMessages", archive)
        self.assertIn("Rigenera alternativa", manager)
        for format_name in ("ToMarkdown", '"json"', '"html"', '"pdf"'):
            with self.subTest(format_name=format_name):
                self.assertIn(format_name, exporter)

    def test_android_system_integrations_and_operational_tabs_exist(self):
        manifest = self.read("src/NemoclawChat.Android/app/src/main/AndroidManifest.xml")
        main = self.read(
            "src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt"
        )
        for component in (
            "HermesWidgetProvider",
            "HermesVoiceTileService",
            "HermesNotificationReplyReceiver",
            "VoiceCallService",
        ):
            with self.subTest(component=component):
                self.assertIn(component, manifest)
        self.assertIn("android.intent.action.SEND", manifest)
        self.assertIn("hermes-hub", manifest)
        for tab in ("Tab.Continuity", "Tab.Audit", "Tab.Artifacts", "Tab.Search"):
            with self.subTest(tab=tab):
                self.assertIn(tab, main)

    def test_voice_call_controls_are_available_on_both_clients(self):
        windows = self.read("src/NemoclawChat.Windows/Pages/VoicePage.xaml")
        android = self.read(
            "src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/VoiceModeScreen.kt"
        )
        for label in ("Interrompi", "Premi per parlare", "Trascrizione", "Wake word"):
            with self.subTest(client="windows", label=label):
                self.assertIn(label, windows)
        for label in ("Interrompi", "PTT", "Trascrizione", "Wake word"):
            with self.subTest(client="android", label=label):
                self.assertIn(label, android)


if __name__ == "__main__":
    unittest.main()
