const statusStyles = {
  Available: 'border-emerald-500/50 bg-emerald-500/15 text-emerald-100',
  Reserved: 'border-amber-400/50 bg-amber-500/15 text-amber-100',
  Occupied: 'border-rose-500/50 bg-rose-500/15 text-rose-100',
  ToClean: 'border-sky-400/50 bg-sky-500/15 text-sky-100',
}

/**
 * @param {object} props
 * @param {any[]} props.placements
 * @param {any[]} props.engagements
 * @param {number | null} props.selectedPlacementId
 * @param {(id: number) => void} props.onSelectPlacement
 */
export function FloorPlanCanvas({ placements, engagements, selectedPlacementId, onSelectPlacement }) {
  const maxX = Math.max(120, ...placements.map((p) => p.layoutX + 110), 600)
  const maxY = Math.max(120, ...placements.map((p) => p.layoutY + 100), 400)

  const activeByPlacement = new Map()
  for (const e of engagements) {
    if (e.status === 'Scheduled' || e.status === 'CheckedIn') {
      if (!activeByPlacement.has(e.placementUnitId)) activeByPlacement.set(e.placementUnitId, [])
      activeByPlacement.get(e.placementUnitId).push(e)
    }
  }

  return (
    <div className="relative overflow-auto rounded-2xl border border-champagne/10 bg-black/25 p-4">
      <div
        className="relative mx-auto"
        style={{ width: maxX + 40, height: maxY + 40, minHeight: 320 }}
      >
        {placements.map((p) => {
          const key = p.status ?? 'Available'
          const styleClass = statusStyles[key] ?? 'border-champagne/20 bg-midnight-2 text-champagne'
          const selected = selectedPlacementId === p.id
          const list = activeByPlacement.get(p.id) ?? []
          const over = list.some((x) => x.rotationOrOverstayFlag)
          return (
            <button
              key={p.id}
              type="button"
              onClick={() => onSelectPlacement(p.id)}
              className={`absolute flex min-h-[72px] w-[100px] flex-col items-center justify-center rounded-xl border-2 px-2 py-2 text-center font-body text-[0.7rem] font-bold uppercase tracking-[0.08em] shadow-lg transition ${styleClass} ${
                selected ? 'ring-2 ring-gold ring-offset-2 ring-offset-midnight' : 'hover:brightness-110'
              }`}
              style={{ left: p.layoutX, top: p.layoutY }}
            >
              {over ? (
                <span className="mb-1 rounded-full bg-gold/90 px-1.5 py-0.5 text-[0.55rem] font-extrabold text-black">
                  Turn
                </span>
              ) : null}
              <span className="line-clamp-2">{p.tableDisplayName}</span>
              <span className="mt-0.5 text-[0.6rem] font-semibold normal-case tracking-normal text-champagne/70">
                {p.minPartyCapacity}–{p.maxPartyCapacity} guests
              </span>
              {p.mergeClusterKey ? (
                <span className="mt-1 text-[0.55rem] normal-case text-champagne/50">Merged</span>
              ) : null}
            </button>
          )
        })}
      </div>
    </div>
  )
}
