/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './**/*.{razor,html,cshtml,cs}',
    './wwwroot/index.html'
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        ground: '#07080a',
        fg: '#ffffff',
        muted: '#9c9c9d',
        key: '#e6e6e6',
        keyInk: '#2f3031',
        crimson: '#ff2f3a',
        coral: '#ff6b4a',
        amber: '#ffb347',
        hairline: 'rgba(255,255,255,0.08)',
        glass: 'rgba(14,15,18,0.72)',
      },
      fontFamily: {
        sans: ['Inter', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'sans-serif'],
        mono: ['GeistMono', 'SFMono-Regular', 'Menlo', 'Monaco', 'Consolas', 'monospace'],
      },
      borderRadius: {
        key: '8px',
        panel: '14px',
      },
      boxShadow: {
        keycap: '0 0 0 2px #000, 0 0 14px rgba(255,255,255,.19), inset 0 1px 0 #fff, inset 0 -1px 0 rgba(0,0,0,.35)',
        keycapActive: '0 0 0 2px #000, inset 0 1px 2px rgba(0,0,0,.4)',
        glass: '0 8px 32px 0 rgba(0, 0, 0, 0.37)',
      },
      animation: {
        'blade-1': 'blade-orbit-1 18s ease-in-out infinite',
        'blade-2': 'blade-orbit-2 22s ease-in-out infinite',
        'blade-3': 'blade-orbit-3 15s ease-in-out infinite',
        'blade-4': 'blade-orbit-4 20s ease-in-out infinite',
      },
      keyframes: {
        'blade-orbit-1': {
          '0%, 100%': { transform: 'translate(0%, 0%) rotate(0deg) scale(1)', opacity: '0.9' },
          '50%': { transform: 'translate(15%, 10%) rotate(45deg) scale(1.15)', opacity: '0.6' },
        },
        'blade-orbit-2': {
          '0%, 100%': { transform: 'translate(0%, 0%) rotate(0deg) scale(1.1)', opacity: '0.8' },
          '50%': { transform: 'translate(-20%, 15%) rotate(-35deg) scale(0.95)', opacity: '0.5' },
        },
        'blade-orbit-3': {
          '0%, 100%': { transform: 'translate(0%, 0%) rotate(0deg) scale(0.95)', opacity: '0.85' },
          '50%': { transform: 'translate(10%, -15%) rotate(60deg) scale(1.2)', opacity: '0.55' },
        },
        'blade-orbit-4': {
          '0%, 100%': { transform: 'translate(0%, 0%) rotate(0deg) scale(1)', opacity: '0.75' },
          '50%': { transform: 'translate(-15%, -10%) rotate(-40deg) scale(1.1)', opacity: '0.45' },
        },
      }
    },
  },
  plugins: [],
}
