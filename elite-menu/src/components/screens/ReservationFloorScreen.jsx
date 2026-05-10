import * as signalR from '@microsoft/signalr'
import { LogIn, RefreshCw, Users } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { FloorPlanCanvas } from '../reservation/FloorPlanCanvas'
import {
  fetchFloorSnapshot,
  floorCheckIn,
  floorMarkClean,
  floorMergePlacements,
  floorRelease,
  getSignalRHubUrl,
} from '../../utils/reservationApi'

function normalizeSnapshot(raw) {
  const placements = (raw?.placements ?? raw?.Placements ?? []).map((p) => ({
    id: p.id ?? p.Id,
    tableId: p.tableId ?? p.TableId,
    tableDisplayName: p.tableDisplayName ?? p.TableDisplayName ?? '',
    minPartyCapacity: p.minPartyCapacity ?? p.MinPartyCapacity ?? 1,
    maxPartyCapacity: p.maxPartyCapacity ?? p.MaxPartyCapacity ?? 4,
    layoutX: p.layoutX ?? p.LayoutX ?? 0,
    layoutY: p.layoutY ?? p.LayoutY ?? 0,
    status: p.status ?? p.Status ?? 'Available',
    mergeClusterKey: p.mergeClusterKey ?? p.MergeClusterKey ?? null,
  }))
  const engagements = (raw?.engagements ?? raw?.Engagements ?? []).map((e) => ({
    id: e.id ?? e.Id,
    placementUnitId: e.placementUnitId ?? e.PlacementUnitId,
    tableId: e.tableId ?? e.TableId,
    tableDisplayName: e.tableDisplayName ?? e.TableDisplayName ?? '',
    plannedStartUtc: e.plannedStartUtc ?? e.PlannedStartUtc,
    plannedEndUtc: e.plannedEndUtc ?? e.PlannedEndUtc,
    actualStartUtc: e.actualStartUtc ?? e.ActualStartUtc ?? null,
    actualEndUtc: e.actualEndUtc ?? e.ActualEndUtc ?? null,
    guestName: e.guestName ?? e.GuestName ?? '',
    guestPhone: e.guestPhone ?? e.GuestPhone ?? '',
    partySize: e.partySize ?? e.PartySize ?? 0,
    status: e.status ?? e.Status ?? '',
    rotationOrOverstayFlag: Boolean(e.rotationOrOverstayFlag ?? e.RotationOrOverstayFlag),
  }))
  return { placements, engagements }
}

export function ReservationFloorScreen() {
  const [snapshot, setSnapshot] = useState(() => ({ placements: [], engagements: [] }))
  const [selectedPlacementId, setSelectedPlacementId] = useState(/** @type {number | null} */ (null))
  const [mergeMode, setMergeMode] = useState(/** @type {number[]} */ ([]))
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [connectionState, setConnectionState] = useState('')

  const token = typeof window !== 'undefined' ? window.sessionStorage.getItem('elite_access_token') : ''

  const reload = useCallback(async () => {
    setError('')
    try {
      const raw = await fetchFloorSnapshot()
      setSnapshot(normalizeSnapshot(raw))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load floor.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    reload()
  }, [reload])

  useEffect(() => {
    if (!token) {
      setConnectionState('offline')
      return
    }
    const url = getSignalRHubUrl('/hubs/reservation-floor')
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => window.sessionStorage.getItem('elite_access_token') || '',
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents,
      })
      .withAutomaticReconnect()
      .build()

    connection.on('floorUpdated', (payload) => {
      setSnapshot(normalizeSnapshot(payload))
    })

    let cancelled = false
    connection
      .start()
      .then(() => connection.invoke('JoinFloor'))
      .then(() => {
        if (!cancelled) setConnectionState('live')
      })
      .catch(() => {
        if (!cancelled) setConnectionState('error')
      })

    return () => {
      cancelled = true
      connection.stop()
    }
  }, [token])

  const selectedEngagement = useMemo(() => {
    if (selectedPlacementId == null) return null
    return (
      snapshot.engagements.find(
        (e) =>
          e.placementUnitId === selectedPlacementId &&
          (e.status === 'Scheduled' || e.status === 'CheckedIn'),
      ) ?? null
    )
  }, [snapshot.engagements, selectedPlacementId])

  const selectedPlacement = useMemo(() => {
    if (selectedPlacementId == null) return null
    return snapshot.placements.find((p) => p.id === selectedPlacementId) ?? null
  }, [snapshot.placements, selectedPlacementId])

  const toggleMerge = (id) => {
    setMergeMode((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]))
  }

  const runMerge = async () => {
    if (mergeMode.length < 2) return
    setError('')
    try {
      await floorMergePlacements(mergeMode, null)
      setMergeMode([])
      await reload()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Merge failed.')
    }
  }

  if (!token) {
    return (
      <main className="min-h-[100svh] bg-midnight px-5 py-10 text-champagne">
        <div className="mx-auto max-w-md rounded-3xl border border-champagne/10 bg-midnight-2 p-6 text-center shadow-xl">
          <LogIn className="mx-auto h-10 w-10 text-gold" />
          <h1 className="mt-4 font-display text-2xl italic">Floor view</h1>
          <p className="mt-2 font-body text-sm text-champagne/65">
            Sign in from the staff hub with your passcode to receive a session token, then return here.
          </p>
          <Link
            to="/staff"
            className="mt-6 inline-flex min-h-[48px] items-center justify-center rounded-xl bg-gold px-6 font-body text-sm font-extrabold uppercase tracking-[0.1em] text-black"
          >
            Open staff hub
          </Link>
        </div>
      </main>
    )
  }

  return (
    <main className="min-h-[100svh] bg-midnight px-4 py-6 text-champagne md:px-8">
      <header className="mb-6 flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div>
          <p className="font-body text-[0.65rem] font-bold uppercase tracking-[0.28em] text-gold/80">Live floor</p>
          <h1 className="font-display text-3xl italic text-champagne">Reservations</h1>
          <p className="mt-1 font-body text-sm text-champagne/55">
            SignalR:{' '}
            <span
              className={
                connectionState === 'live' ? 'text-emerald-400' : connectionState === 'error' ? 'text-red-400' : 'text-champagne/60'
              }
            >
              {connectionState || '…'}
            </span>
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => reload()}
            className="inline-flex items-center gap-2 rounded-xl border border-champagne/15 bg-midnight-2 px-4 py-2 font-body text-xs font-bold uppercase tracking-[0.14em] text-champagne/80"
          >
            <RefreshCw className="h-4 w-4" />
            Refresh
          </button>
          <Link
            to="/staff"
            className="inline-flex items-center gap-2 rounded-xl border border-gold/30 px-4 py-2 font-body text-xs font-bold uppercase tracking-[0.14em] text-gold"
          >
            <Users className="h-4 w-4" />
            Staff hub
          </Link>
        </div>
      </header>

      {loading ? (
        <p className="font-body text-sm text-champagne/60">Loading floor…</p>
      ) : (
        <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
          <FloorPlanCanvas
            placements={snapshot.placements}
            engagements={snapshot.engagements}
            selectedPlacementId={selectedPlacementId}
            onSelectPlacement={(id) => {
              setSelectedPlacementId(id)
            }}
          />

          <aside className="rounded-2xl border border-champagne/10 bg-midnight-2/80 p-4 shadow-lg">
            <h2 className="font-body text-xs font-bold uppercase tracking-[0.2em] text-gold/80">Table actions</h2>
            {error ? (
              <p className="mt-3 rounded-xl border border-red-500/25 bg-red-500/10 px-3 py-2 font-body text-xs text-red-200">
                {error}
              </p>
            ) : null}

            {selectedPlacement ? (
              <div className="mt-4 space-y-3 font-body text-sm text-champagne/80">
                <p className="font-display text-xl italic text-champagne">{selectedPlacement.tableDisplayName}</p>
                <p>Status: {selectedPlacement.status}</p>
                <button
                  type="button"
                  onClick={() => toggleMerge(selectedPlacement.id)}
                  className="w-full rounded-lg border border-champagne/15 py-2 text-xs font-bold uppercase tracking-[0.1em] text-champagne/70"
                >
                  {mergeMode.includes(selectedPlacement.id) ? 'Remove from merge' : 'Add to merge'}
                </button>
                {mergeMode.length > 0 ? (
                  <p className="text-xs text-champagne/55">
                    Merge set: {mergeMode.join(', ')} — need two or more
                  </p>
                ) : null}
                <button
                  type="button"
                  disabled={mergeMode.length < 2}
                  onClick={runMerge}
                  className="w-full rounded-lg bg-gold/90 py-2 text-xs font-extrabold uppercase tracking-[0.1em] text-black disabled:opacity-40"
                >
                  Merge selected
                </button>

                {selectedEngagement ? (
                  <div className="mt-4 space-y-2 border-t border-champagne/10 pt-4">
                    <p className="text-xs font-bold uppercase tracking-[0.16em] text-champagne/50">Active booking</p>
                    <p>{selectedEngagement.guestName}</p>
                    <p className="text-xs text-champagne/60">{selectedEngagement.guestPhone}</p>
                    <p className="text-xs">
                      Party {selectedEngagement.partySize} · {selectedEngagement.status}
                    </p>
                    {selectedEngagement.rotationOrOverstayFlag ? (
                      <p className="rounded-lg bg-gold/15 px-2 py-1 text-xs font-semibold text-gold">Overstay / rotation</p>
                    ) : null}
                    <div className="grid gap-2">
                      {selectedEngagement.status === 'Scheduled' ? (
                        <button
                          type="button"
                          onClick={async () => {
                            try {
                              await floorCheckIn(selectedEngagement.id)
                              await reload()
                            } catch (e) {
                              setError(e instanceof Error ? e.message : 'Check-in failed')
                            }
                          }}
                          className="rounded-lg bg-emerald-600/90 py-2 text-xs font-bold uppercase tracking-[0.08em]"
                        >
                          Check in
                        </button>
                      ) : null}
                      {selectedEngagement.status === 'CheckedIn' ? (
                        <button
                          type="button"
                          onClick={async () => {
                            try {
                              await floorRelease(selectedEngagement.id)
                              await reload()
                            } catch (e) {
                              setError(e instanceof Error ? e.message : 'Release failed')
                            }
                          }}
                          className="rounded-lg bg-champagne/20 py-2 text-xs font-bold uppercase tracking-[0.08em]"
                        >
                          Release table
                        </button>
                      ) : null}
                    </div>
                  </div>
                ) : null}

                {selectedPlacement.status === 'ToClean' ? (
                  <button
                    type="button"
                    onClick={async () => {
                      try {
                        await floorMarkClean(selectedPlacement.id)
                        await reload()
                      } catch (e) {
                        setError(e instanceof Error ? e.message : 'Mark clean failed')
                      }
                    }}
                    className="mt-4 w-full rounded-lg border border-sky-400/40 py-2 text-xs font-bold uppercase tracking-[0.08em] text-sky-200"
                  >
                    Mark clean
                  </button>
                ) : null}
              </div>
            ) : (
              <p className="mt-4 font-body text-sm text-champagne/50">Select a table on the floor plan.</p>
            )}
          </aside>
        </div>
      )}
    </main>
  )
}
