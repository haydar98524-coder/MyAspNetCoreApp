/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Views/**/*.cshtml",
    "./wwwroot/js/**/*.js",
    "./Areas/**/*.cshtml"
  ],
  theme: {
    extend: {
      fontFamily: {
        cairo: ["Cairo", "Tahoma", "system-ui", "sans-serif"]
      },
      colors: {
        brand: {
          50: "#f0faf7",
          100: "#d7f2ea",
          200: "#b0e5d7",
          300: "#7dd3bf",
          400: "#45b8a1",
          500: "#219c87",
          600: "#0f7f6f",
          700: "#0f655a",
          800: "#105149",
          900: "#0f433e",
          950: "#032b28"
        },
        gold: {
          50: "#fdf9eb",
          100: "#faf0c8",
          200: "#f4df8f",
          300: "#eecb55",
          400: "#e2b32b",
          500: "#c9a227",
          600: "#a67c1e",
          700: "#855a1a",
          800: "#70481c",
          900: "#603c1c"
        },
        sand: {
          50: "#fbfaf6",
          100: "#f7f5ee",
          200: "#efeadd",
          300: "#e3dbc7"
        },
        ink: {
          100: "#f1f5f9",
          200: "#e2e8f0",
          300: "#cbd5e1",
          400: "#94a3b8",
          500: "#64748b",
          600: "#475569",
          700: "#33413f",
          800: "#1e293b",
          900: "#0f1f1d"
        }
      },
      boxShadow: {
        card: "0 1px 2px rgba(15,31,29,.04), 0 8px 24px -12px rgba(15,31,29,.12)",
        pop: "0 18px 45px -18px rgba(15,31,29,.35)"
      }
    }
  },
  plugins: []
};
