# Idea — elastic pipeline steps

*Last updated: 2026-08-25*

> A pipeline whose steps choose their own transport: a direct call while the step keeps up, a queue once it
> does not, so the slow step scales alone instead of the whole app.
> Purpose — record the idea and what it would cost, before any of it is built.
> Use case — a multi-step flow where one step's latency degrades every request behind it.

## The gap it targets

Entry-level backpressure is the tool we have, and it solves a different problem.

- a queue plus a concurrency limit at the pipeline's entry absorbs a **burst of requests**
- it does nothing for **one slow step**: the burst is admitted, then piles up behind step 3
- scaling the app scales all 5 steps, so 4 of them get capacity they never needed
- the connections, memory and DB pool that come with those 4 are pure waste

The insight is that elasticity wants a boundary per step, not per app.

## Shape

A step's transport is an implementation detail as long as the pipeline owns the step's contract.

| Mode | What links the steps | When |
|---|---|---|
| direct | an in-process call to the next step | every step keeps up |
| queued | the step publishes, a consumer elsewhere runs it, the reply correlates back | one step falls behind |

Switching is per step, not per pipeline. Steps 1, 2, 4 and 5 stay direct calls in the origin process while
step 3 is published to a subject that N scaled workers consume — so scaling adds workers that run *only*
step 3, and the origin process keeps its connection count.

The switch is either configured, or driven by what the pipeline already measures.

- configured — a host declares `step 3 runs queued`, which is the version to build first
- self-tuning — the pipeline watches per-step duration and in-flight depth and flips the mode itself

## What it costs

Each item below is a real constraint, not a caveat to wave through.

- **The caller's contract changes.** A queued step makes the flow asynchronous end to end. Either the
  caller stops awaiting a value, or the queued hop keeps a correlation and waits — which is
  request/reply, and it inherits the reply timeout as a new failure mode.
- **Delivery semantics change.** A direct call runs exactly once; a queued hop is at-least-once, so a step
  that flips mode must already be idempotent. This is a property of the step, checkable before the flip.
- **Ordering changes.** Direct steps preserve order by construction; N consumers do not. A step that
  depends on order can never flip, so the contract needs a way to say so.
- **Serialization appears.** Each queued hop pays a serialize / deserialize round trip and the payload
  must be serializable — which some in-process step inputs are not.
- **Self-tuning can oscillate.** A step near the threshold flips back and forth, and each flip changes
  semantics. Any automatic mode needs hysteresis and a floor on how often it may switch.

## What already exists to build it on

The SDK carries most of the parts, which is what makes this worth writing down rather than dismissing.

- `IPipelineBehavior` / `IConsumeFilter` — the step boundary the switch would sit at
- `RequestClient` — request/reply over a transport, which is the correlation a queued hop needs
- `IdempotencyBehavior` — the guard a step must pass before it is allowed to flip
- `MessagingMetrics` — per-destination duration and in-flight counters, the input a self-tuning mode reads
- `IDelayedDeliveryService`, the outbox and the dead-letter path — what a queued hop falls back on

## Open questions

- Does a step declare its own eligibility (`idempotent`, `order-free`, `serializable`), or does the host?
- Is the unit a step, or a *segment* of consecutive steps — since two adjacent slow steps in one hop pay
  serialization once instead of twice?
- What does a queued step do with the ambient context a direct call carried for free — the trace, the
  tenant, the current user?
- Does the origin process keep the request open while a queued step runs, or hand back a handle?

## Status

Idea only. Nothing built, nothing scheduled. The configured mode is the honest first slice; the
self-tuning mode is a second one that should not be attempted until per-step metrics are proven.
