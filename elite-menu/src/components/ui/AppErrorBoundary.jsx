import { Component } from 'react'

export class AppErrorBoundary extends Component {
  /** @param {{ children: import('react').ReactNode }} props */
  constructor(props) {
    super(props)
    this.state = /** @type {{ error: Error | null }} */ ({ error: null })
  }

  static getDerivedStateFromError(error) {
    return { error: error instanceof Error ? error : new Error(String(error)) }
  }

  render() {
    if (this.state.error) {
      const msg = this.state.error.message
      const stack = import.meta.env.DEV ? this.state.error.stack : undefined
      return (
        <div className="min-h-[100svh] bg-midnight px-6 py-10 text-champagne">
          <p className="font-body text-xs font-bold uppercase tracking-[0.2em] text-gold">Something went wrong</p>
          <h1 className="mt-3 font-display text-2xl italic">The menu couldn&apos;t render</h1>
          <pre className="mt-4 overflow-x-auto rounded-xl border border-red-500/30 bg-black/30 p-4 font-mono text-xs text-red-200">
            {msg}
          </pre>
          {stack ? (
            <pre className="mt-3 max-h-[40vh] overflow-auto whitespace-pre-wrap rounded-xl border border-champagne/10 bg-black/20 p-4 font-mono text-[0.65rem] text-champagne/50">
              {stack}
            </pre>
          ) : null}
          <button
            type="button"
            className="mt-6 rounded-xl border border-gold/40 px-4 py-3 font-body text-sm font-semibold text-gold"
            onClick={() => window.location.reload()}
          >
            Reload page
          </button>
        </div>
      )
    }

    return this.props.children
  }
}
