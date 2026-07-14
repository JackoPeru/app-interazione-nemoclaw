import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]


class ProjectWorkspaceContractTests(unittest.TestCase):
    def read(self, relative: str) -> str:
        return (ROOT / relative).read_text(encoding="utf-8")

    def test_windows_project_navigation_is_not_archive_alias(self):
        shell = self.read("src/NemoclawChat.Windows/MainWindow.xaml")
        code = self.read("src/NemoclawChat.Windows/MainWindow.xaml.cs")
        self.assertIn('Click="Projects_Click"', shell)
        self.assertIn("ContentFrame.Navigate(typeof(ProjectsPage))", code)

    def test_workspace_fields_round_trip_across_clients_and_gateway(self):
        fields = (
            "projectId",
            "workspacePath",
            "repositoryUrl",
            "projectInstructions",
            "projectMemory",
            "authorizedTools",
        )
        windows_gateway = self.read("src/NemoclawChat.Windows/Services/GatewayService.cs")
        android = self.read("src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt")
        gateway_patch = self.read("scripts/patch-hermes-gateway-native.py")
        for field in fields:
            with self.subTest(field=field):
                self.assertIn(field, windows_gateway)
                self.assertIn(field, android)
                self.assertIn(f'"{field}"', gateway_patch)

    def test_active_project_context_reaches_native_requests(self):
        windows_protocol = self.read("src/NemoclawChat.Windows/Services/HermesHubProtocol.cs")
        android_stream = self.read("src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/ChatStream.kt")
        android_main = self.read("src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt")
        self.assertIn("project_context = project is null ? null", windows_protocol)
        self.assertIn("ProjectContextInstructions(settings)", windows_protocol)
        self.assertIn('"project_context"', android_stream)
        self.assertIn("projectContextInstructions(settings)", android_stream)
        self.assertIn("projectId = settings.activeProjectId", android_main)


if __name__ == "__main__":
    unittest.main()
