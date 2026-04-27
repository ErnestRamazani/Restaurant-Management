/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,jsx}'],
  theme: {
    extend: {
      colors: {
        midnight: '#0F1923',
        champagne: '#EDE8DC',
        gold: '#C9A84C',
        'gold-light': '#D4B86A',
        'midnight-2': '#161E27',
        'midnight-3': '#1C2732',
      },
      fontFamily: {
        display: ['"Playfair Display"', 'serif'],
        body: ['"DM Sans"', 'sans-serif'],
        mono: ['"DM Mono"', 'monospace'],
      },
      keyframes: {
        shimmer: {
          '0%': { backgroundPosition: '200% 0' },
          '100%': { backgroundPosition: '-200% 0' },
        },
        pulseSlow: {
          '0%, 100%': { opacity: 0.45, transform: 'translateY(0)' },
          '50%': { opacity: 1, transform: 'translateY(2px)' },
        },
      },
      animation: {
        shimmer: 'shimmer 4s linear infinite',
        'pulse-slow': 'pulseSlow 2.2s ease-in-out infinite',
      },
    },
  },
  plugins: [],
}
