function ChevronIcon({ direction = 'down' }) {
  const rotation = direction === 'right' ? '-90' : '0';

  return (
    <svg aria-hidden="true" className="icon" viewBox="0 0 24 24">
      <path
        d="M6 9l6 6 6-6"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
        transform={`rotate(${rotation} 12 12)`}
      />
    </svg>
  );
}

export default ChevronIcon;
