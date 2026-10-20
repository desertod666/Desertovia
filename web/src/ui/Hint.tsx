import { useEffect, useId, useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

const MARGIN = 8

let closeOpenPopup: (() => void) | null = null

/** One top-bar popup is open at a time: opening this one closes whichever was open before. */
export function useSinglePopup(open: boolean, close: () => void) {
  useEffect(() => {
    if (!open) return
    closeOpenPopup?.()
    closeOpenPopup = close
    return () => {
      if (closeOpenPopup === close) closeOpenPopup = null
    }
  }, [open])
}

/**
 * A game term the player can click or tap to read how it works.
 * One hint is open at a time; a click elsewhere or Escape closes it.
 */
export function Hint({ text, children, className, label }: { text: ReactNode; children: ReactNode; className?: string; label?: string }) {
  const [open, setOpen] = useState(false)
  const [position, setPosition] = useState<CSSProperties>({ visibility: 'hidden' })
  const termRef = useRef<HTMLButtonElement>(null)
  const popupRef = useRef<HTMLSpanElement>(null)
  const id = useId()
  useSinglePopup(open, () => setOpen(false))

  useEffect(() => {
    if (!open) return
    const close = () => setOpen(false)
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node
      if (!termRef.current?.contains(target) && !popupRef.current?.contains(target)) close()
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.stopPropagation()
      close()
    }
    // A fixed portal must not stay floating over an unrelated row after its anchor scrolls away.
    const onScroll = (event: Event) => {
      if (!popupRef.current?.contains(event.target as Node)) close()
    }
    document.addEventListener('pointerdown', onPointerDown, true)
    window.addEventListener('keydown', onKeyDown, true)
    window.addEventListener('resize', close)
    document.addEventListener('scroll', onScroll, true)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown, true)
      window.removeEventListener('keydown', onKeyDown, true)
      window.removeEventListener('resize', close)
      document.removeEventListener('scroll', onScroll, true)
    }
  }, [open])

  useLayoutEffect(() => {
    if (!open || !termRef.current || !popupRef.current) return
    const term = termRef.current.getBoundingClientRect()
    const popup = popupRef.current.getBoundingClientRect()
    const above = term.top - popup.height - MARGIN >= 0 || term.bottom + popup.height + MARGIN > window.innerHeight
    const left = Math.min(Math.max(MARGIN, term.left + term.width / 2 - popup.width / 2), window.innerWidth - popup.width - MARGIN)
    setPosition({
      left,
      top: above ? Math.max(MARGIN, term.top - popup.height - 6) : term.bottom + 6,
    })
  }, [open])

  const toggle = () => {
    setPosition({ visibility: 'hidden' })
    setOpen((value) => !value)
  }

  return (
    <>
      <button
        ref={termRef}
        type="button"
        className={`hint-term${open ? ' open' : ''}${className ? ` ${className}` : ''}`}
        aria-label={label}
        aria-expanded={open}
        aria-controls={open ? id : undefined}
        aria-describedby={open ? id : undefined}
        onClick={(event) => {
          event.stopPropagation()
          toggle()
        }}
      >
        {children}
      </button>
      {open &&
        createPortal(
          <span ref={popupRef} id={id} role="tooltip" className="hint-popup" style={position}>
            {text}
          </span>,
          // Keep hints inside the accessible modal subtree when aria-modal hides the rest of the page.
          termRef.current?.closest('[role="dialog"]') ?? document.body,
        )}
    </>
  )
}
