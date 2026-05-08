# Work in Progress

Implementing GraphQL subscriptions to prevent multiple tabs for the same RefTest. The solution tracks sessions in the backend and notifies other clients in real-time when a session is locked or unlocked.

## Plan:
- Add GraphQL `sessionUpdated` subscription to track session state.
- Create `openRefTestSession` and `closeRefTestSession` mutations.
- Update frontend to handle subscription updates and enforce restrictions.