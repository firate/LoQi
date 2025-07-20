import type {Metadata} from "next";
import {Geist, Geist_Mono} from "next/font/google";
import "./globals.css";

const geistSans = Geist({
    variable: "--font-geist-sans",
    subsets: ["latin"],
});

const geistMono = Geist_Mono({
    variable: "--font-geist-mono",
    subsets: ["latin"],
});

export const metadata: Metadata = {
    title: "LoQI - Log Viewer",
    description: "Log viewer and search application",
    icons: {
        icon: '../logo/favicon.svg',
        shortcut: '../logo/favicon.svg',
        apple: '../logo/favicon.svg',
    },
};

export default function RootLayout({children,}: Readonly<{
    children: React.ReactNode;
}>) {
    return (
        <html lang="en">
            <head>
                <link rel="icon" href="../logo/favicon.svg" type="image/svg+xml" />
                <link rel="shortcut icon" href="../logo/favicon.svg"/>
            </head>
            <body className={`${geistSans.variable} ${geistMono.variable} antialiased`} >
                {children}
            </body>
        </html>
    );
}
