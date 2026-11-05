import {themes as prismThemes} from 'prism-react-renderer';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Ariadne.NET',
  tagline: 'AI generates behavior. The engine governs structure.',
  favicon: 'img/favicon.ico',

  future: {
    v4: true,
  },

  url: 'https://MarcelRoozekrans.github.io',
  baseUrl: '/Ariadne.NET/',

  organizationName: 'MarcelRoozekrans',
  projectName: 'Ariadne.NET',

  onBrokenLinks: 'warn',

  markdown: {
    mermaid: true,
    format: 'md',
    hooks: {
      onBrokenMarkdownLinks: 'warn',
    },
  },
  themes: ['@docusaurus/theme-mermaid'],

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: './sidebars.js',
          routeBasePath: '/',
          editUrl:
            'https://github.com/MarcelRoozekrans/Ariadne.NET/tree/main/docs-site/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      colorMode: {
        respectPrefersColorScheme: true,
      },
      navbar: {
        title: 'Ariadne.NET',
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'docsSidebar',
            position: 'left',
            label: 'Docs',
          },
          {
            href: 'https://github.com/MarcelRoozekrans/Ariadne.NET',
            label: 'GitHub',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Documentation',
            items: [
              {label: 'Introduction', to: '/'},
              {label: 'Features', to: '/features'},
              {label: 'How It Works', to: '/how-it-works/overview'},
            ],
          },
          {
            title: 'More',
            items: [
              {
                label: 'GitHub',
                href: 'https://github.com/MarcelRoozekrans/Ariadne.NET',
              },
              {
                label: 'NuGet (ZInject)',
                href: 'https://www.nuget.org/packages/ZInject',
              },
            ],
          },
        ],
        copyright: `Copyright ${new Date().getFullYear()} Marcel Roozekrans. Built with Docusaurus.`,
      },
      prism: {
        theme: prismThemes.github,
        darkTheme: prismThemes.dracula,
        additionalLanguages: ['csharp', 'yaml', 'bash', 'json'],
      },
    }),
};

export default config;
