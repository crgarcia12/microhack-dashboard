import type { Metadata } from "next";
import "./globals.css";
import ThemeProvider from "@/theme/ThemeProvider";
import { AuthProvider } from "@/contexts/AuthContext";

export const metadata: Metadata = {
  title: "MicroHack",
  description: "MicroHack event management portal",
};

const frontendContainerVersion = "0.0.1";
const backendContainerVersion = "0.0.1";

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <head>
        <script dangerouslySetInnerHTML={{ __html: `
          // Remove Next.js route announcer custom element to prevent aria-live conflicts
          (function() {
            function fix() {
              var els = document.getElementsByTagName('next-route-announcer');
              for (var i = els.length - 1; i >= 0; i--) els[i].remove();
            }
            setInterval(fix, 100);
            if (typeof MutationObserver !== 'undefined') {
              new MutationObserver(fix).observe(document.documentElement, { childList: true, subtree: true });
            }
          })();
        `}} />
      </head>
      <body>
        <ThemeProvider>
          <AuthProvider>
            {children}
          </AuthProvider>
        </ThemeProvider>
        <div
          aria-hidden="true"
          style={{
            position: "fixed",
            bottom: "6px",
            left: "50%",
            transform: "translateX(-50%)",
            fontSize: "10px",
            lineHeight: 1,
            opacity: 0.65,
            zIndex: 9999,
            pointerEvents: "none",
            fontFamily: "monospace",
          }}
        >
          FE {frontendContainerVersion} | BE {backendContainerVersion}
        </div>
      </body>
    </html>
  );
}
