export interface ApiError { error: { code: string; message: string } }

/** A page of a server-side paginated list. */
export interface Paged<T> { totalCount: number; page: number; pageSize: number; items: T[]; }
/** A plain (non-paginated) list envelope. */
export interface ItemsOf<T> { items: T[]; }
/** Acknowledgement of a command, with the id of the created resource when there is one. */
export interface ApiMessage { message: string; id?: string; }
