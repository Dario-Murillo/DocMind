import { ActivatedRouteSnapshot, BaseRouteReuseStrategy } from '@angular/router';

// The router normally keeps the component alive when only a route parameter changes. Workspace
// relies on the opposite: a new notebook means a new Workspace, with a fresh Conversations service,
// so a late response from the previous notebook lands on an instance nobody is looking at.
export class NotebookRouteReuse extends BaseRouteReuseStrategy {
  override shouldReuseRoute(
    future: ActivatedRouteSnapshot,
    current: ActivatedRouteSnapshot,
  ): boolean {
    return (
      super.shouldReuseRoute(future, current) &&
      future.paramMap.get('notebookId') === current.paramMap.get('notebookId')
    );
  }
}
