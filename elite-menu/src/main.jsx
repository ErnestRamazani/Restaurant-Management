import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { AppErrorBoundary } from './components/ui/AppErrorBoundary.jsx'
import './i18n.js'
import './index.css'
import App from './App.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <AppErrorBoundary>
      <App />
    </AppErrorBoundary>
  </StrictMode>,
)
