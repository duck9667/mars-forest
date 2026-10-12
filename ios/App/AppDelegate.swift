import UIKit
import WebKit

// 번들 안 Web/ 폴더를 mars://app/… 으로 서빙한다.
// file:// 대신 커스텀 스킴을 쓰는 이유: ES 모듈·importmap이 동작하고, localStorage 저장이 고정 origin(mars://app)에 안정적으로 남는다.
final class LocalSchemeHandler: NSObject, WKURLSchemeHandler {
    private let root = Bundle.main.resourceURL!.appendingPathComponent("Web", isDirectory: true)

    func webView(_ webView: WKWebView, start task: WKURLSchemeTask) {
        guard let url = task.request.url else { return }
        var path = url.path
        if path.isEmpty || path == "/" { path = "/index.html" }
        let file = root.appendingPathComponent(String(path.dropFirst()))
        guard let data = try? Data(contentsOf: file) else {
            task.didReceive(HTTPURLResponse(url: url, statusCode: 404, httpVersion: "HTTP/1.1", headerFields: nil)!)
            task.didFinish()
            return
        }
        let mime: String
        switch file.pathExtension.lowercased() {
        case "html": mime = "text/html; charset=utf-8"
        case "js", "mjs": mime = "text/javascript; charset=utf-8"
        case "css": mime = "text/css; charset=utf-8"
        case "json": mime = "application/json; charset=utf-8"
        case "png": mime = "image/png"
        case "jpg", "jpeg": mime = "image/jpeg"
        case "svg": mime = "image/svg+xml"
        case "woff2": mime = "font/woff2"
        default: mime = "application/octet-stream"
        }
        let headers = ["Content-Type": mime, "Content-Length": String(data.count), "Cache-Control": "no-cache"]
        task.didReceive(HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: headers)!)
        task.didReceive(data)
        task.didFinish()
    }

    func webView(_ webView: WKWebView, stop task: WKURLSchemeTask) {}
}

final class GameViewController: UIViewController {
    private var web: WKWebView!
    override var prefersStatusBarHidden: Bool { true }
    override var prefersHomeIndicatorAutoHidden: Bool { true }
    override var supportedInterfaceOrientations: UIInterfaceOrientationMask { .portrait }

    override func viewDidLoad() {
        super.viewDidLoad()
        let bg = UIColor(red: 0.933, green: 0.498, blue: 0.247, alpha: 1) // #ee7f3f
        view.backgroundColor = bg

        let cfg = WKWebViewConfiguration()
        cfg.setURLSchemeHandler(LocalSchemeHandler(), forURLScheme: "mars")
        cfg.allowsInlineMediaPlayback = true
        cfg.mediaTypesRequiringUserActionForPlayback = []

        web = WKWebView(frame: .zero, configuration: cfg)
        web.isOpaque = false
        web.backgroundColor = bg
        web.scrollView.isScrollEnabled = false
        web.scrollView.bounces = false
        web.scrollView.contentInsetAdjustmentBehavior = .never // 게임이 env(safe-area-inset-*)로 직접 처리
        web.allowsBackForwardNavigationGestures = false
        web.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(web)
        NSLayoutConstraint.activate([
            web.topAnchor.constraint(equalTo: view.topAnchor),
            web.bottomAnchor.constraint(equalTo: view.bottomAnchor),
            web.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            web.trailingAnchor.constraint(equalTo: view.trailingAnchor),
        ])
        web.load(URLRequest(url: URL(string: "mars://app/index.html")!))
    }
}

@main
final class AppDelegate: UIResponder, UIApplicationDelegate {
    var window: UIWindow?

    func application(_ application: UIApplication,
                     didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil) -> Bool {
        let w = UIWindow(frame: UIScreen.main.bounds)
        w.rootViewController = GameViewController()
        w.makeKeyAndVisible()
        window = w
        return true
    }
}
