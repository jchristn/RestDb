function Shell({ sidebar, topbar, children }) {
  return (
    <div className="workspace-shell">
      <aside className="workspace-shell__sidebar">{sidebar}</aside>
      <main className="workspace-shell__main">
        <div className="workspace-shell__topbar">{topbar}</div>
        <div className="workspace-shell__content">{children}</div>
      </main>
    </div>
  );
}

export default Shell;
