import type { SchemeDocument } from "./types";

export function crossingTraffic(document: SchemeDocument) {
  const traffic = new Map<
    string,
    { passages: number; weightedSeconds: number }
  >();
  for (const plan of document.plans)
    for (const chain of plan.chains)
      for (const step of chain.steps) {
        if (!step.incomingLinkId) continue;
        const total = traffic.get(step.incomingLinkId) ?? {
          passages: 0,
          weightedSeconds: 0,
        };
        total.passages += plan.quantity;
        total.weightedSeconds += plan.quantity * step.travelSeconds;
        traffic.set(step.incomingLinkId, total);
      }
  return traffic;
}
